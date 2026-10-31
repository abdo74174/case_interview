using CaseInterview.Api.Middleware;
using CaseInterview.Application;
using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Domain.Entities;
using CaseInterview.Infrastructure;
using CaseInterview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/caseinterview-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add Controllers and Exception Handling
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Swagger with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CaseInterview API",
        Version = "v1",
        Description = "Realistic Case-Based System Design Mock Interview Platform API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token in format: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Migrate Database on startup if available
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CaseInterviewDbContext>();
        if (dbContext.Database.IsNpgsql())
        {
            logger.LogInformation("Applying pending database migrations...");
            dbContext.Database.Migrate();
            logger.LogInformation("Database migration completed successfully.");
        }
        else
        {
            logger.LogInformation("Using InMemory database; skipping relational migrations.");
        }

        // Seed default demo users if database is empty
        if (!dbContext.Users.Any())
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var adminUser = new User
            {
                Email = "admin@caseinterview.io",
                FullName = "Chief Architect (Admin)",
                Role = "Admin",
                PasswordHash = passwordHasher.HashPassword("AdminPassword123!"),
                CreatedAtUtc = DateTime.UtcNow
            };

            var candidateUser = new User
            {
                Email = "architect@caseinterview.io",
                FullName = "Candidate Engineer",
                Role = "User",
                PasswordHash = passwordHasher.HashPassword("Password123!"),
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.AddRange(adminUser, candidateUser);
            dbContext.SaveChanges();
            logger.LogInformation("Seeded default users: admin@caseinterview.io (Admin) and architect@caseinterview.io (User)");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not automatically migrate database at startup. Ensure PostgreSQL container is running.");
    }
}

// Request pipeline
app.UseExceptionHandler();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CaseInterview API v1");
    });
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
