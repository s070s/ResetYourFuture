using ResetYourFuture.Api.Logging;
using ResetYourFuture.Shared.Models;

var builder = WebApplication.CreateBuilder(args);
var clientOrigin = builder.Configuration["AllowedClientOrigin"]?? "https://localhost:7083";

// Add file-based logging
builder.Logging.AddFileLogger("Logs");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// register CORS BEFORE Build
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", p => p.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// use the exact policy name
app.UseCors("BlazorClient");

// Get logger for request logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started. Logs directory: {LogsPath}", Path.GetFullPath("Logs"));

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


