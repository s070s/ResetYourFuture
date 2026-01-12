using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResetYourFuture.Api.Data;
using ResetYourFuture.Api.Domain.Entities;
using ResetYourFuture.Api.Domain.Enums;
using ResetYourFuture.Api.Identity;
using ResetYourFuture.Api.Logging;
using ResetYourFuture.Api.Services;
using ResetYourFuture.Shared.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var clientOrigin = config["AllowedClientOrigin"] ?? "https://localhost:7083";

// --- Logging ---
builder.Logging.AddFileLogger("Logs");

// --- Database (SQLite for local dev, no server required) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(config.GetConnectionString("DefaultConnection")));

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false; // Relaxed for usability; tighten in prod
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true; // Email confirmation required
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- JWT Authentication ---
var jwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = config["Jwt:Issuer"];
var jwtAudience = config["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero // No tolerance for token expiry
    };
});

// --- Authorization Policies ---
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));

// --- Services ---
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", p => p
        .WithOrigins(clientOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // Required for auth headers
});

var app = builder.Build();

// --- Auto-create database in development ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Environment.IsDevelopment())
    {
        // Creates database and schema automatically (no migrations needed for dev)
        db.Database.EnsureCreated();
    }

    // --- Seed Roles ---
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = ["Admin", "Student"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // --- Seed Admin User (Development only) ---
    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = config["AdminUser:Email"] ?? "admin@resetyourfuture.local";
        var adminPassword = config["AdminUser:Password"] ?? "Admin123!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true, // Pre-confirmed for dev
                GdprConsentGiven = true,
                GdprConsentDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        // --- Seed Sample Courses (Development only) ---
        if (!await db.Courses.AnyAsync())
        {
            var course1 = new Course
            {
                Id = Guid.NewGuid(),
                Title = "Reset Your Future: Career Discovery",
                Description = "A comprehensive program to help you discover your career path and develop essential skills for the modern job market.",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                Modules =
                [
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Title = "Introduction to Career Planning",
                        Description = "Learn the fundamentals of career planning and self-assessment.",
                        SortOrder = 1,
                        CreatedAt = DateTime.UtcNow,
                        Lessons =
                        [
                            new Lesson
                            {
                                Id = Guid.NewGuid(),
                                Title = "Welcome to the Course",
                                ContentType = ContentType.Video,
                                Content = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                                DurationMinutes = 5,
                                SortOrder = 1,
                                CreatedAt = DateTime.UtcNow
                            },
                            new Lesson
                            {
                                Id = Guid.NewGuid(),
                                Title = "Understanding Your Values",
                                ContentType = ContentType.Text,
                                Content = "# Understanding Your Values\n\nYour values are the foundation of a fulfilling career. In this lesson, we'll explore:\n\n## What are Values?\n\nValues are the principles and beliefs that guide your decisions and behavior. They represent what's truly important to you.\n\n## Why Values Matter in Career Planning\n\n1. **Job Satisfaction**: When your work aligns with your values, you feel more fulfilled.\n2. **Decision Making**: Values help you make difficult career choices.\n3. **Motivation**: Working in alignment with your values increases intrinsic motivation.\n\n## Exercise: Identify Your Top 5 Values\n\nTake a moment to reflect on what matters most to you. Consider areas like:\n- Family and relationships\n- Financial security\n- Creativity and innovation\n- Helping others\n- Personal growth\n- Work-life balance",
                                DurationMinutes = 15,
                                SortOrder = 2,
                                CreatedAt = DateTime.UtcNow
                            }
                        ]
                    },
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Title = "Skills Assessment",
                        Description = "Identify and evaluate your current skills and competencies.",
                        SortOrder = 2,
                        CreatedAt = DateTime.UtcNow,
                        Lessons =
                        [
                            new Lesson
                            {
                                Id = Guid.NewGuid(),
                                Title = "Hard Skills vs Soft Skills",
                                ContentType = ContentType.Video,
                                Content = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                                DurationMinutes = 10,
                                SortOrder = 1,
                                CreatedAt = DateTime.UtcNow
                            },
                            new Lesson
                            {
                                Id = Guid.NewGuid(),
                                Title = "Transferable Skills",
                                ContentType = ContentType.Text,
                                Content = "# Transferable Skills\n\nTransferable skills are abilities you can use in any job or industry.\n\n## Common Transferable Skills\n\n- **Communication**: Writing, speaking, presenting\n- **Leadership**: Managing projects, mentoring others\n- **Problem Solving**: Analytical thinking, creativity\n- **Organization**: Time management, planning\n- **Technology**: Digital literacy, software proficiency\n\n## How to Identify Your Transferable Skills\n\n1. Review your past experiences (work, education, volunteering)\n2. List the tasks you performed\n3. Identify the underlying skills used\n4. Match these to potential career paths",
                                DurationMinutes = 12,
                                SortOrder = 2,
                                CreatedAt = DateTime.UtcNow
                            }
                        ]
                    }
                ]
            };

            var course2 = new Course
            {
                Id = Guid.NewGuid(),
                Title = "Building Your Personal Brand",
                Description = "Learn how to create and maintain a professional personal brand that opens doors to new opportunities.",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                Modules =
                [
                    new Module
                    {
                        Id = Guid.NewGuid(),
                        Title = "Personal Branding Basics",
                        Description = "Understanding the fundamentals of personal branding.",
                        SortOrder = 1,
                        CreatedAt = DateTime.UtcNow,
                        Lessons =
                        [
                            new Lesson
                            {
                                Id = Guid.NewGuid(),
                                Title = "What is Personal Branding?",
                                ContentType = ContentType.Text,
                                Content = "# What is Personal Branding?\n\nPersonal branding is the practice of defining and promoting what you stand for.\n\n## Key Elements\n\n- **Identity**: Who you are and what makes you unique\n- **Value Proposition**: What you offer to employers or clients\n- **Visibility**: How you present yourself online and offline\n\n## Why It Matters\n\nIn today's competitive job market, a strong personal brand can:\n- Differentiate you from other candidates\n- Attract opportunities to you\n- Build trust and credibility",
                                DurationMinutes = 10,
                                SortOrder = 1,
                                CreatedAt = DateTime.UtcNow
                            }
                        ]
                    }
                ]
            };

            db.Courses.AddRange(course1, course2);
            await db.SaveChangesAsync();
        }
    }
}

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("BlazorClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// --- Logger ---
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started. Logs: {LogsPath}", Path.GetFullPath("Logs"));

// --- Demo endpoint (keep for now) ---
app.MapGet("/api/students", () =>
{
    logger.LogInformation("GET /api/students called");
    var students = new List<Student>
    {
        new Student(1, "George", "Kokkalis", 25, "Athens", "Career Counselor"),
        new Student(2, "Maria", "Papadopoulou", 30, "Thessaloniki", "Engineer"),
        new Student(3, "Dimitris", "Nikolaidis", 28, "Patras", "Teacher"),
        new Student(4, "Elena", "Vasilaki", 35, "Heraklion", "Doctor"),
        new Student(5, "Kostas", "Georgiou", 22, "Larissa", "Student")
    };
    return students;
})
.WithName("GetStudents");

app.Run();


