using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResetYourFuture.Web.Data
{
    /// <summary>
    /// Design-time factory for EF tools (dotnet ef) to create ApplicationDbContext.
    /// Reads connection string from environment or appsettings.json.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext( string [] args )
        {
            var env = Environment.GetEnvironmentVariable( "ASPNETCORE_ENVIRONMENT" ) ?? "Development";
            var builder = new ConfigurationBuilder()
                .SetBasePath( Directory.GetCurrentDirectory() )
                .AddJsonFile( "appsettings.json" , optional: true )
                .AddJsonFile( $"appsettings.{env}.json" , optional: true )
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            var connectionString = configuration.GetConnectionString( "DefaultConnection" );
            if ( string.IsNullOrWhiteSpace( connectionString ) )
                connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=ResetYourFutureDb;Trusted_Connection=True;TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer( connectionString )
                .Options;

            return new ApplicationDbContext( options );
        }
    }
}