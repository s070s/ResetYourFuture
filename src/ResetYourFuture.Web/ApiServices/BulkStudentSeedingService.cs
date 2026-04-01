using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using ResetYourFuture.Web.Identity;

namespace ResetYourFuture.Web.ApiServices;

/// <summary>
/// Runs the bulk student seeder as a hosted background service so it does not
/// block application startup. The seeder only runs in Development when
/// SeedData:Enabled is true.
/// </summary>
public sealed class BulkStudentSeedingService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BulkStudentSeedingService> _logger;

    public BulkStudentSeedingService(
        IServiceProvider services ,
        IConfiguration config ,
        IWebHostEnvironment env ,
        ILogger<BulkStudentSeedingService> logger )
    {
        _services = services;
        _config = config;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        if ( !_env.IsDevelopment() || !_config.GetValue<bool>( "SeedData:Enabled" ) )
            return;

        // Brief delay so the app is fully started and accepting requests before
        // the expensive seeding work begins.
        await Task.Delay( TimeSpan.FromSeconds( 3 ) , stoppingToken );

        using var scope = _services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var bulkCount = _config.GetValue<int>( "SeedData:BulkStudentCount" , 10_000 );
        var studentPassword = _config [ "SeedData:StudentPassword" ] ?? "Student123!";

        await BulkStudentSeeder.SeedAsync( userManager , bulkCount , studentPassword , _logger , stoppingToken );
    }
}
