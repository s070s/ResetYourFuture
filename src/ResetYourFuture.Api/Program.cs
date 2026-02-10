using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Api.Logging;
using ResetYourFuture.Api.Services;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder( args );
var config = builder.Configuration;
var clientOrigin = config [ "AllowedClientOrigin" ] ?? "https://localhost:7083";

// --- Logging ---
builder.Logging.AddFileLogger( "Logs" );

// --- Database (SQL Server for dev & Azure) ---
builder.Services.AddDbContext<ApplicationDbContext>( options =>
    options.UseSqlServer( config.GetConnectionString( "DefaultConnection" ) ) );

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser , IdentityRole>( options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false; // Relaxed for usability; tighten in prod
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true; // Email confirmation required
} )
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- JWT Authentication ---
var jwtKey = config [ "Jwt:Key" ] ?? throw new InvalidOperationException( "JWT Key not configured" );
var jwtIssuer = config [ "Jwt:Issuer" ];
var jwtAudience = config [ "Jwt:Audience" ];

builder.Services.AddAuthentication( options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
} )
.AddJwtBearer( options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true ,
        ValidateAudience = true ,
        ValidateLifetime = true ,
        ValidateIssuerSigningKey = true ,
        ValidIssuer = jwtIssuer ,
        ValidAudience = jwtAudience ,
        IssuerSigningKey = new SymmetricSecurityKey( Encoding.UTF8.GetBytes( jwtKey ) ) ,
        ClockSkew = TimeSpan.Zero // No tolerance for token expiry
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<ApplicationUser>>();
            var userId = context.Principal?.FindFirstValue( ClaimTypes.NameIdentifier );
            if ( userId is not null )
            {
                var user = await userManager.FindByIdAsync( userId );
                if ( user is null || !user.IsEnabled )
                {
                    context.Fail( "Account is disabled." );
                    context.HttpContext.Items["UserDisabled"] = true;
                }
            }
        } ,
        OnChallenge = context =>
        {
            if ( context.HttpContext.Items.ContainsKey( "UserDisabled" ) )
            {
                context.Response.Headers["X-User-Disabled"] = "true";
            }
            return Task.CompletedTask;
        }
    };
} );

// --- Authorization Policies ---
builder.Services.AddAuthorizationBuilder()
    .AddPolicy( "AdminOnly" , policy => policy.RequireRole( "Admin" ) )
    .AddPolicy( "StudentOnly" , policy => policy.RequireRole( "Student" ) );

// --- Services ---
builder.Services.AddScoped<ITokenService , TokenService>();
builder.Services.AddScoped<IFileStorage , LocalFileStorage>();
builder.Services.AddScoped<IEmailService , StubEmailService>();

// --- Localization ---
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>( options =>
{
    var supportedCultures = new [] { "en" , "el" };
    options.SetDefaultCulture( "en" )
        .AddSupportedCultures( supportedCultures )
        .AddSupportedUICultures( supportedCultures );
} );

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- CORS ---
builder.Services.AddCors( options =>
{
    options.AddPolicy( "BlazorClient" , p => p
        .WithOrigins( clientOrigin )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials() // Required for auth headers
        .WithExposedHeaders( "X-User-Disabled" ) );
} );

var app = builder.Build();

// --- Apply migrations and seed (runs at startup) ---
using ( var scope = app.Services.CreateScope() )
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Apply EF Core migrations (creates database if it doesn't exist)
        db.Database.Migrate();
        startupLogger.LogInformation( "Applied EF Core migrations successfully." );
    }
    catch ( Exception ex )
    {
        // Fail fast so deployment surfaces migration issues
        startupLogger.LogCritical( ex , "Applying EF Core migrations failed." );
        throw;
    }

    // --- Seed Roles ---
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string [] roles = new [] { "Admin" , "Student" };
    foreach ( var role in roles )
    {
        if ( !await roleManager.RoleExistsAsync( role ) )
        {
            await roleManager.CreateAsync( new IdentityRole( role ) );
        }
    }

    // --- Seed Admin User (Development only) ---
    if ( app.Environment.IsDevelopment() )
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = config [ "AdminUser:Email" ] ?? "admin@resetyourfuture.local";
        var adminPassword = config [ "AdminUser:Password" ] ?? "Admin123!";

        if ( await userManager.FindByEmailAsync( adminEmail ) is null )
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail ,
                Email = adminEmail ,
                FirstName = "System" ,
                LastName = "Administrator" ,
                EmailConfirmed = true , // Pre-confirmed for dev
                IsEnabled = true ,
                GdprConsentGiven = true ,
                GdprConsentDate = DateTime.UtcNow ,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync( admin , adminPassword );
            if ( result.Succeeded )
            {
                await userManager.AddToRoleAsync( admin , "Admin" );
            }
        }

        // --- Seed Sample Courses from JSON (Development only) ---
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Read path from configuration; fall back to a sensible relative path when missing.
        // Use app.Environment.ContentRootPath so relative paths are resolved from the application's content root.
        var jsonSeedPath = config.GetValue<string>( "SeedData:JsonPaths:Courses" )
                           ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Courses" );

        await CourseSeeder.SeedFromJsonAsync( db , jsonSeedPath , seedLogger );

        // --- Seed Assessments from JSON (Development only) ---
        var assessmentJsonPath = config.GetValue<string>( "SeedData:JsonPaths:Assessments" )
                                 ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Assessments" );

        await AssessmentSeeder.SeedFromJsonAsync( db , assessmentJsonPath , seedLogger );
    }
}

// --- Pipeline ---
if ( app.Environment.IsDevelopment() )
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors( "BlazorClient" );
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// --- Logger ---
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation( "Application started. Logs: {LogsPath}" , Path.GetFullPath( "Logs" ) );

app.Run();





