using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResetYourFuture.Api.Data
{
    /// <summary>
    /// Design-time factory for EF tools (dotnet ef) to create ApplicationDbContext.
    /// Reads connection string from environment or appsettings.json.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext( string [] args )
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath( Directory.GetCurrentDirectory() )
                .AddJsonFile( "appsettings.json" , optional: true )
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            var connectionString = configuration.GetConnectionString( "DefaultConnection" )
                ?? "Data Source=(localdb)\\MSSQLLocalDB;Integrated Security=True;";

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer( connectionString )
                .Options;

            return new ApplicationDbContext( options );
        }
    }
}