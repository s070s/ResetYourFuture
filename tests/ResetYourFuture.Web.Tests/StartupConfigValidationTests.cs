using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ResetYourFuture.Web.Startup;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Web.Tests;

public class StartupConfigValidationTests
{
    private static WebApplicationBuilder NewBuilder(string environment, Dictionary<string, string?> config)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(config);
        return builder;
    }

    private static Dictionary<string, string?> ValidProductionConfig() => new()
    {
        ["Jwt:Key"] = "production-signing-key-at-least-32-bytes-long-1234567890",
        ["AdminUser:Password"] = "Admin-Pass-1!",
        ["Email:Smtp:Host"] = "smtp.example.com"
    };

    [Fact]
    public void Valid_Production_DoesNotThrow()
    {
        var builder = NewBuilder("Production", ValidProductionConfig());

        Should.NotThrow(() => builder.ValidateRequiredConfig());
    }

    [Fact]
    public void Valid_Development_DoesNotRequireEmailHost()
    {
        var config = ValidProductionConfig();
        config.Remove("Email:Smtp:Host");
        var builder = NewBuilder("Development", config);

        Should.NotThrow(() => builder.ValidateRequiredConfig());
    }

    [Fact]
    public void MissingJwtKey_Throws()
    {
        var config = ValidProductionConfig();
        config.Remove("Jwt:Key");
        var builder = NewBuilder("Production", config);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("Jwt:Key");
    }

    [Fact]
    public void ShortJwtKey_Throws()
    {
        var config = ValidProductionConfig();
        config["Jwt:Key"] = "too-short";
        var builder = NewBuilder("Production", config);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("32 bytes");
    }

    [Fact]
    public void MissingAdminPassword_Throws()
    {
        var config = ValidProductionConfig();
        config.Remove("AdminUser:Password");
        var builder = NewBuilder("Production", config);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("AdminUser:Password");
    }

    [Fact]
    public void Production_MissingEmailHost_Throws()
    {
        var config = ValidProductionConfig();
        config.Remove("Email:Smtp:Host");
        var builder = NewBuilder("Production", config);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("Email:Smtp:Host");
    }

    [Fact]
    public void Development_SeedDataEnabled_MissingStudentPassword_Throws()
    {
        var config = ValidProductionConfig();
        config.Remove("Email:Smtp:Host");
        config["SeedData:Enabled"] = "true";
        var builder = NewBuilder("Development", config);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("SeedData:StudentPassword");
    }

    [Fact]
    public void Development_SeedDataEnabled_WithStudentPassword_DoesNotThrow()
    {
        var config = ValidProductionConfig();
        config.Remove("Email:Smtp:Host");
        config["SeedData:Enabled"] = "true";
        config["SeedData:StudentPassword"] = "Student-Pass-1!";
        var builder = NewBuilder("Development", config);

        Should.NotThrow(() => builder.ValidateRequiredConfig());
    }

    [Fact]
    public void MultipleMissingKeys_ReportsAllInOneException()
    {
        var builder = NewBuilder("Production", []);

        var ex = Should.Throw<InvalidOperationException>(() => builder.ValidateRequiredConfig());
        ex.Message.ShouldContain("Jwt:Key");
        ex.Message.ShouldContain("AdminUser:Password");
        ex.Message.ShouldContain("Email:Smtp:Host");
    }
}
