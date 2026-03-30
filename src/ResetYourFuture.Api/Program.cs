using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Hubs;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Interfaces;
using ResetYourFuture.Api.Logging;
using ResetYourFuture.Api.Services;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder( args );
var config = builder.Configuration;

// --- Logging ---
builder.Logging.AddFileLogger( "Logs" );

// --- Database (SQL Server for dev & Azure) ---
builder.Services.AddDbContext<ApplicationDbContext>( options =>
    options.UseSqlServer( config.GetConnectionString( "DefaultConnection" ) ,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5 ,
            maxRetryDelay: TimeSpan.FromSeconds( 10 ) ,
            errorNumbersToAdd: null ) ) );

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
        // Allow SignalR and media asset requests to receive JWT from query string.
        // WebSocket connections and browser <video>/<audio> elements cannot send custom headers.
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if ( !string.IsNullOrEmpty( accessToken ) &&
                 ( path.StartsWithSegments( "/hubs/chat" ) ||
                   path.StartsWithSegments( "/api/lessons" ) ) )
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        } ,
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue( ClaimTypes.NameIdentifier );
            if ( userId is not null )
            {
                var cache = context.HttpContext.RequestServices
                    .GetRequiredService<IMemoryCache>();
                var cacheKey = $"user_enabled_{userId}";

                if ( !cache.TryGetValue( cacheKey , out bool isEnabled ) )
                {
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.FindByIdAsync( userId );
                    isEnabled = user is not null && user.IsEnabled;
                    cache.Set( cacheKey , isEnabled , TimeSpan.FromSeconds( 60 ) );
                }

                if ( !isEnabled )
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
builder.Services.AddScoped<ISubscriptionService , SubscriptionService>();
builder.Services.AddScoped<ICertificateService , CertificateService>();
builder.Services.AddScoped<IBlogArticleService , BlogArticleService>();

// --- Localization ---
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>( options =>
{
    var supportedCultures = new [] { "en" , "el" };
    options.SetDefaultCulture( "en" )
        .AddSupportedCultures( supportedCultures )
        .AddSupportedUICultures( supportedCultures );
} );

builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHostedService<BulkStudentSeedingService>();

// --- CORS ---
var clientOrigin = config [ "AllowedClientOrigins" ]
    ?? throw new InvalidOperationException( "AllowedClientOrigins not configured." );

builder.Services.AddCors( options =>
{
    options.AddPolicy( "BlazorClient" , p =>
    {
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials() // Required for auth headers
         .WithExposedHeaders( "X-User-Disabled" );

        if ( builder.Environment.IsDevelopment() )
        {
            // Allow localhost and any VS dev-tunnel origin (*.devtunnels.ms) in development
            p.SetIsOriginAllowed( origin =>
                origin == clientOrigin ||
                ( Uri.TryCreate( origin , UriKind.Absolute , out var uri ) &&
                  uri.Host.EndsWith( ".devtunnels.ms" , StringComparison.OrdinalIgnoreCase ) ) );
        }
        else
        {
            p.WithOrigins( clientOrigin );
        }
    } );
} );

var app = builder.Build();

// --- Pre-warm LocalDB so EF Core doesn't hit LOCALDB_ERROR_CANNOT_GET_USER_PROFILE_FOLDER ---
// LocalDB auto-stops when idle. The auto-start via connection string fails with 0x89C5010A
// on Windows 11 if the user profile folder can't be resolved in the app's process context.
// Running 'sqllocaldb start' explicitly before EF connects works around this reliably.
if ( app.Environment.IsDevelopment() )
{
    try
    {
        using var proc = System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "sqllocaldb",
            Arguments              = "start MSSQLLocalDB",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        } );
        proc?.WaitForExit( 10_000 );
    }
    catch { /* sqllocaldb not on PATH — non-fatal, LocalDB may already be running */ }
}

// --- Apply migrations and seed (runs at startup) ---
using ( var scope = app.Services.CreateScope() )
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Detect a database that exists but was created outside EF Core Migrations
        // (e.g. via EnsureCreated or manual scripts). In that case the schema is
        // already present but __EFMigrationsHistory is empty, so MigrateAsync would
        // fail trying to re-create existing tables.
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();

        if ( !appliedMigrations.Any() && pendingMigrations.Any() && await db.Database.CanConnectAsync() )
        {
            if ( app.Environment.IsDevelopment() )
            {
                startupLogger.LogWarning(
                    "Database exists without migration history. " +
                    "Dropping and recreating to apply migrations cleanly." );
                await db.Database.EnsureDeletedAsync();
            }
            else
            {
                throw new InvalidOperationException(
                    "Database exists but has no EF Core migration history. " +
                    "Manually reconcile the schema or drop and recreate the database." );
            }
        }

        await db.Database.MigrateAsync();
        startupLogger.LogInformation( "EF Core migrations applied (database created if needed)." );
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

    // --- Seed Subscription Plans ---
    await SubscriptionPlanSeeder.SeedAsync( db , startupLogger );

    // --- Seed Blog Articles ---
    await BlogArticleSeeder.SeedAsync( db , startupLogger );

    // --- Seed Admin User (all environments) ---
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
            EmailConfirmed = true ,
            IsEnabled = true ,
            GdprConsentGiven = true ,
            GdprConsentDate = DateTime.UtcNow ,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync( admin , adminPassword );
        if ( result.Succeeded )
        {
            await userManager.AddToRoleAsync( admin , "Admin" );
            startupLogger.LogInformation( "Seeded admin user '{Email}'." , adminEmail );
        }
    }

    // --- Development-only seed data (Courses, Assessments, Students) ---
    if ( app.Environment.IsDevelopment() && config.GetValue<bool>( "SeedData:Enabled" ) )
    {
        // --- Seed Sample Courses from JSON ---
        var jsonSeedPath = config.GetValue<string>( "SeedData:JsonPaths:Courses" )
                           ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Courses" );

        await CourseSeeder.SeedFromJsonAsync( db , jsonSeedPath , startupLogger );

        // --- Seed Assessments from JSON ---
        var assessmentJsonPath = config.GetValue<string>( "SeedData:JsonPaths:Assessments" )
                                 ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Assessments" );

        await AssessmentSeeder.SeedFromJsonAsync( db , assessmentJsonPath , startupLogger );

        // --- Seed Student Users from JSON ---
        var studentJsonPath = config.GetValue<string>( "SeedData:JsonPaths:Students" )
                              ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Students" );
        var studentPassword = config [ "SeedData:StudentPassword" ] ?? "Student123!";
        await StudentSeeder.SeedFromJsonAsync( userManager , studentJsonPath , studentPassword , startupLogger );

        // Bulk student generation runs in BulkStudentSeedingService (background) so it
        // does not delay app startup.
    }
}

// --- Pipeline ---
if ( app.Environment.IsDevelopment() )
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors( "BlazorClient" );
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>( "/hubs/chat" );

// --- Logger ---
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation( "Application started. Logs: {LogsPath}" , Path.GetFullPath( "Logs" ) );

app.Run();





