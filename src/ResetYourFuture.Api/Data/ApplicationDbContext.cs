using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ResetYourFuture.Api.Identity;

namespace ResetYourFuture.Api.Data;

/// <summary>
/// EF Core DbContext with ASP.NET Identity configured for ApplicationUser.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            // Ignore computed property
            entity.Ignore(u => u.Age);

            // Store DateOfBirth as DATE column
            entity.Property(u => u.DateOfBirth)
                  .HasConversion(
                      d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      d => d.HasValue ? DateOnly.FromDateTime(d.Value) : null)
                  .HasColumnType("date");

            // Store Status as int
            entity.Property(u => u.Status)
                  .HasConversion<int>();

            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
        });
    }
}
