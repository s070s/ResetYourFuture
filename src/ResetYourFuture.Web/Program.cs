using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Web.Data;
using ResetYourFuture.Web.Domain.Entities;
using ResetYourFuture.Web.Hubs;
using ResetYourFuture.Web.Identity;
using ResetYourFuture.Web.ApiInterfaces;
using ResetYourFuture.Web.ApiServices;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Web.Services;
using ResetYourFuture.Web.Logging;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;
using System.Text;

// Bring App and Routes razor components into scope
using ResetYourFuture.Web;

var builder = WebApplication.CreateBuilder( args );
var config = builder.Configuration;

// --- Logging ---
builder.Logging.AddFileLogger( "Logs" );

// --- Database (SQL Server) ---
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
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
} )
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- Authentication: MultiAuth policy (Cookie for Blazor pages, JWT for API/SignalR) ---
var jwtKey = config [ "Jwt:Key" ] ?? throw new InvalidOperationException( "JWT Key not configured" );
var jwtIssuer = config [ "Jwt:Issuer" ];
var jwtAudience = config [ "Jwt:Audience" ];

builder.Services.AddAuthentication( options =>
{
    // MultiAuth selects JWT when Authorization header is present, Cookie otherwise.
    // This allows the same [Authorize] attribute to work for both Blazor pages and API controllers.
    //
    // IMPORTANT: AddIdentity (called above) sets DefaultAuthenticateScheme = "Identity.Application"
    // internally. We must explicitly override DefaultAuthenticateScheme here — setting only
    // DefaultScheme is not enough because DefaultAuthenticateScheme takes precedence when non-null.
    // Without this, ServerAuthenticationStateProvider authenticates via "Identity.Application",
    // which cannot read the .RYF.Auth cookie, and every Blazor circuit sees an anonymous user.
    options.DefaultScheme = "MultiAuth";
    options.DefaultAuthenticateScheme = "MultiAuth";
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
} )
.AddPolicyScheme( "MultiAuth" , "MultiAuth" , options =>
{
    options.ForwardDefaultSelector = ctx =>
    {
        var authHeader = ctx.Request.Headers.Authorization.ToString();
        if ( !string.IsNullOrEmpty( authHeader ) && authHeader.StartsWith( "Bearer " , StringComparison.OrdinalIgnoreCase ) )
            return JwtBearerDefaults.AuthenticationScheme;
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
} )
.AddCookie( CookieAuthenticationDefaults.AuthenticationScheme , options =>
{
    options.Cookie.Name = ".RYF.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.ExpireTimeSpan = TimeSpan.FromDays( 7 );
    options.SlidingExpiration = true;

    // In development allow the cookie over plain HTTP
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
} )
.AddJwtBearer( JwtBearerDefaults.AuthenticationScheme , options =>
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
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        // Allow SignalR and media asset requests to receive JWT from query string.
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query [ "access_token" ];
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
                    context.HttpContext.Items [ "UserDisabled" ] = true;
                }
            }
        } ,
        OnChallenge = context =>
        {
            if ( context.HttpContext.Items.ContainsKey( "UserDisabled" ) )
            {
                context.Response.Headers [ "X-User-Disabled" ] = "true";
            }
            return Task.CompletedTask;
        }
    };
} );

// --- Authorization Policies ---
builder.Services.AddAuthorizationBuilder()
    .AddPolicy( "AdminOnly" , policy => policy.RequireRole( "Admin" ) )
    .AddPolicy( "StudentOnly" , policy => policy.RequireRole( "Student" ) );

// --- API Services ---
builder.Services.AddScoped<ITokenService , TokenService>();
builder.Services.AddScoped<IFileStorage , LocalFileStorage>();
builder.Services.AddScoped<IEmailService , StubEmailService>();
builder.Services.AddScoped<ISubscriptionService , SubscriptionService>();
builder.Services.AddScoped<ICertificateService , CertificateService>();
builder.Services.AddScoped<IBlogArticleService , BlogArticleService>();
builder.Services.AddScoped<ITestimonialService , TestimonialService>();

// --- Web Services ---
builder.Services.AddScoped<IAuthService , AuthService>();

// --- SSR API Handler (attaches JWT from cookie claims for loopback HttpClient calls) ---
builder.Services.AddTransient<SsrApiHandler>();

// --- Consumer registrations (server-side HttpClient calling the same host) ---
var selfBase = config [ "SelfBaseUrl" ] ?? "https://localhost:7090";

// Named client for dev-only endpoints (no auth handler needed)
builder.Services.AddHttpClient( "SelfClient" , c => c.BaseAddress = new Uri( selfBase ) );

builder.Services.AddHttpClient<ICourseConsumer , CourseConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAssessmentConsumer , AssessmentConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<ISubscriptionConsumer , SubscriptionConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IProfileConsumer , ProfileConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminAnalyticsConsumer , AdminAnalyticsConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminUserConsumer , AdminUserConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminCourseConsumer , AdminCourseConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminModuleConsumer , AdminModuleConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminLessonConsumer , AdminLessonConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IAdminAssessmentConsumer , AdminAssessmentConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<ICertificateConsumer , CertificateConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IBlogConsumer , BlogConsumer>( c => c.BaseAddress = new Uri( selfBase ) );
builder.Services.AddHttpClient<IAdminBlogConsumer , AdminBlogConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<ITestimonialConsumer , TestimonialConsumer>( c => c.BaseAddress = new Uri( selfBase ) );
builder.Services.AddHttpClient<IAdminTestimonialConsumer , AdminTestimonialConsumer>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();
builder.Services.AddHttpClient<IChatService , ChatService>( c => c.BaseAddress = new Uri( selfBase ) )
    .AddHttpMessageHandler<SsrApiHandler>();

// --- Localization ---
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>( options =>
{
    var supportedCultures = new [] { "en-GB" , "el-GR" };
    options.SetDefaultCulture( "en-GB" )
        .AddSupportedCultures( supportedCultures )
        .AddSupportedUICultures( supportedCultures );
} );

builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHostedService<BulkStudentSeedingService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection();

// --- Blazor SSR ---
// AddCascadingAuthenticationState registers the ServerAuthenticationStateProvider
// which reads auth state from the Blazor Server circuit's connection principal.
// Without this, AuthenticationState cascades as anonymous even when the auth
// cookie is present, because the interactive circuit cannot access HttpContext.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Suppress noisy info-level authorization logs
builder.Logging.AddFilter( "Microsoft.AspNetCore.Authorization" , LogLevel.Warning );

var app = builder.Build();

// --- Pre-warm LocalDB ---
if ( app.Environment.IsDevelopment() )
{
    try
    {
        using var proc = System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sqllocaldb" ,
            Arguments = "start MSSQLLocalDB" ,
            RedirectStandardOutput = true ,
            RedirectStandardError = true ,
            UseShellExecute = false ,
            CreateNoWindow = true ,
        } );
        proc?.WaitForExit( 10_000 );
    }
    catch { /* sqllocaldb not on PATH — non-fatal */ }
}

// --- Seed ---
using ( var scope = app.Services.CreateScope() )
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Seed Roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string [] roles = [ "Admin" , "Student" ];
    foreach ( var role in roles )
    {
        if ( !await roleManager.RoleExistsAsync( role ) )
            await roleManager.CreateAsync( new IdentityRole( role ) );
    }

    // Seed Subscription Plans
    await SubscriptionPlanSeeder.SeedAsync( db , startupLogger );

    // Seed Blog Articles
    await BlogArticleSeeder.SeedAsync( db , startupLogger );

    // Seed Admin User
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

    // Development-only seed data
    if ( app.Environment.IsDevelopment() && config.GetValue<bool>( "SeedData:Enabled" ) )
    {
        var jsonSeedPath = config.GetValue<string>( "SeedData:JsonPaths:Courses" )
                           ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Courses" );
        await CourseSeeder.SeedFromJsonAsync( db , jsonSeedPath , startupLogger );

        var assessmentJsonPath = config.GetValue<string>( "SeedData:JsonPaths:Assessments" )
                                 ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Assessments" );
        await AssessmentSeeder.SeedFromJsonAsync( db , assessmentJsonPath , startupLogger );

        var studentJsonPath = config.GetValue<string>( "SeedData:JsonPaths:Students" )
                              ?? Path.Combine( app.Environment.ContentRootPath , ".." , "ResetYourFuture.Shared" , "JSON" , "Students" );
        var studentPassword = config [ "SeedData:StudentPassword" ] ?? "Student123!";
        await StudentSeeder.SeedFromJsonAsync( userManager , studentJsonPath , studentPassword , startupLogger );
    }
}

// --- Pipeline ---
if ( app.Environment.IsDevelopment() )
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();

// Middleware: redirect disabled users accessing Blazor pages
app.Use( async ( context , next ) =>
{
    await next();
    // JWT bearer sets UserDisabled for API requests; cookie auth re-validates on Blazor pages.
    // If a user's account is disabled and they somehow reach a protected page,
    // the CookieAuthenticationHandler's ValidatePrincipal should handle it.
    // The X-User-Disabled header path is for API/SignalR consumers that still use JWT.
} );

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// --- Culture endpoint ---
app.MapGet( "/culture/set" , ( string culture , string? returnUrl , HttpContext ctx ) =>
{
    ctx.Response.Cookies.Append(
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName ,
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
            new Microsoft.AspNetCore.Localization.RequestCulture( culture ) ) ,
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears( 1 ) , IsEssential = true } );

    // NavigationManager.Uri passes an absolute URL; extract the local path so
    // Results.LocalRedirect (which only accepts local paths) doesn't throw.
    var redirect = "/";
    if ( !string.IsNullOrEmpty( returnUrl ) )
    {
        if ( Uri.TryCreate( returnUrl , UriKind.Absolute , out var absolute ) )
            redirect = absolute.PathAndQuery;
        else if ( returnUrl.StartsWith( '/' ) )
            redirect = returnUrl;
    }

    return Results.LocalRedirect( redirect );
} );

// --- Auth completion endpoints ---
// Blazor Server circuits run after the HTTP response has already been committed, so
// cookie operations (SignInAsync / SignOutAsync) must happen on a separate fresh request.
// Components issue NavigationManager.NavigateTo(url, forceLoad: true) with a signed
// DataProtection token.  These endpoints decode the token, do a single DB lookup to
// rebuild the ClaimsPrincipal, then call SignInAsync / SignOutAsync normally.

app.MapGet( "/auth/complete" , async (
    string ticket ,
    string? returnUrl ,
    HttpContext ctx ,
    IDataProtectionProvider dpProvider ,
    UserManager<ApplicationUser> userManager ,
    ISubscriptionService subscriptionService ,
    ILoggerFactory loggerFactory ) =>
{
    var logger = loggerFactory.CreateLogger( "AuthCompletion" );

    // --- Decode the signed token -------------------------------------------------
    string payload;
    try
    {
        var protector = dpProvider
            .CreateProtector( ResetYourFuture.Web.Services.AuthService.ProtectorPurpose )
            .ToTimeLimitedDataProtector();
        payload = protector.Unprotect( ticket );
    }
    catch ( Exception ex )
    {
        logger.LogWarning( ex , "Auth completion: token invalid or expired." );
        return Results.LocalRedirect( "/login?error=session_expired" );
    }

    // Format: "{userId}|{adminBackupId or empty}|{0 or 1 for deleteAdminBackup}"
    var parts = payload.Split( '|' );
    if ( parts.Length != 3 )
    {
        logger.LogWarning( "Auth completion: token had unexpected format." );
        return Results.LocalRedirect( "/login?error=session_expired" );
    }

    var userId = parts [ 0 ];
    var adminBackupId = string.IsNullOrEmpty( parts [ 1 ] ) ? null : parts [ 1 ];
    var deleteAdminBackup = parts [ 2 ] == "1";

    // --- Rebuild principal from DB -----------------------------------------------
    var user = await userManager.FindByIdAsync( userId );
    if ( user is null )
    {
        logger.LogWarning( "Auth completion: user {UserId} not found." , userId );
        return Results.LocalRedirect( "/login" );
    }

    var roles = await userManager.GetRolesAsync( user );
    SubscriptionTierEnum tier;
    try
    {
        tier = await subscriptionService.GetUserTierAsync( userId );
    }
    catch ( Exception ex )
    {
        logger.LogWarning( ex , "Auth completion: GetUserTierAsync failed for {UserId} — defaulting to Free." , userId );
        tier = SubscriptionTierEnum.Free;
    }

    var claims = new List<Claim>
    {
        new( ClaimTypes.NameIdentifier , user.Id ) ,
        new( ClaimTypes.Email , user.Email! ) ,
        new( "firstName" , user.FirstName ) ,
        new( "lastName" , user.LastName ) ,
        new( "isEnabled" , user.IsEnabled.ToString().ToLowerInvariant() ) ,
        new( "subscriptionTier" , ((int)tier).ToString() )
    };
    claims.AddRange( roles.Select( r => new Claim( ClaimTypes.Role , r ) ) );
    if ( !string.IsNullOrEmpty( adminBackupId ) )
        claims.Add( new Claim( "impersonatedBy" , adminBackupId ) );

    var identity = new ClaimsIdentity( claims , CookieAuthenticationDefaults.AuthenticationScheme );
    var principal = new ClaimsPrincipal( identity );

    // --- Write / clear admin backup cookie ---------------------------------------
    if ( !string.IsNullOrEmpty( adminBackupId ) )
    {
        ctx.Response.Cookies.Append(
            ResetYourFuture.Web.Services.AuthService.AdminBackupCookieName ,
            adminBackupId ,
            new CookieOptions
            {
                HttpOnly = true ,
                SameSite = SameSiteMode.Lax ,
                Expires = DateTimeOffset.UtcNow.AddHours( 8 ) ,
                IsEssential = true
            } );
    }
    if ( deleteAdminBackup )
        ctx.Response.Cookies.Delete( ResetYourFuture.Web.Services.AuthService.AdminBackupCookieName );

    // --- Issue auth cookie -------------------------------------------------------
    await ctx.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme ,
        principal ,
        new AuthenticationProperties
        {
            IsPersistent = true ,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays( 7 )
        } );

    logger.LogInformation( "User {UserId} signed in via /auth/complete." , userId );

    var redirect = "/";
    if ( !string.IsNullOrEmpty( returnUrl ) )
    {
        if ( Uri.TryCreate( returnUrl , UriKind.Absolute , out var abs ) )
            redirect = abs.PathAndQuery;
        else if ( returnUrl.StartsWith( '/' ) )
            redirect = returnUrl;
    }

    return Results.LocalRedirect( redirect );
} ).AllowAnonymous();

app.MapGet( "/auth/signout" , async ( string? returnUrl , HttpContext ctx ) =>
{
    await ctx.SignOutAsync( CookieAuthenticationDefaults.AuthenticationScheme );
    ctx.Response.Cookies.Delete( ResetYourFuture.Web.Services.AuthService.AdminBackupCookieName );

    var redirect = "/";
    if ( !string.IsNullOrEmpty( returnUrl ) )
    {
        if ( Uri.TryCreate( returnUrl , UriKind.Absolute , out var abs ) )
            redirect = abs.PathAndQuery;
        else if ( returnUrl.StartsWith( '/' ) )
            redirect = returnUrl;
    }

    return Results.LocalRedirect( redirect );
} ).AllowAnonymous();

app.MapControllers();
app.MapHub<ChatHub>( "/hubs/chat" );
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// --- Sitemap ---
app.MapGet( "/sitemap.xml" , async ( IBlogArticleService blog , HttpContext ctx ) =>
{
    const string baseUrl = "https://reset-your-future.com";
    var articles = await blog.GetPublishedSummariesAsync( 200 , "en" , ctx.RequestAborted );

    var sb = new System.Text.StringBuilder();
    sb.AppendLine( "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" );
    sb.AppendLine( "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" );

    sb.AppendLine( $"  <url><loc>{baseUrl}/</loc><priority>1.0</priority><changefreq>weekly</changefreq></url>" );

    foreach ( var article in articles )
    {
        var lastmod = article.PublishedAt?.ToString( "yyyy-MM-dd" ) ?? DateTime.UtcNow.ToString( "yyyy-MM-dd" );
        sb.AppendLine( $"  <url><loc>{baseUrl}/blog/{article.Slug}</loc><lastmod>{lastmod}</lastmod><priority>0.8</priority><changefreq>monthly</changefreq></url>" );
    }

    sb.AppendLine( "</urlset>" );
    ctx.Response.ContentType = "application/xml; charset=utf-8";
    await ctx.Response.WriteAsync( sb.ToString() );
} ).AllowAnonymous();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation( "ResetYourFuture.Web started. Logs: {LogsPath}" , Path.GetFullPath( "Logs" ) );

app.Run();
