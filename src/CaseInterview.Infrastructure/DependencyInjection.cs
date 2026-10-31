using System.ClientModel;
using System.Text;
using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Infrastructure.AI;
using CaseInterview.Infrastructure.BackgroundProcessing;
using CaseInterview.Infrastructure.Chunker;
using CaseInterview.Infrastructure.Pdf;
using CaseInterview.Infrastructure.Persistence;
using CaseInterview.Infrastructure.Security;
using CaseInterview.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenAI;

namespace CaseInterview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Persistence & PostgreSQL with pgvector (or InMemory for test environments)
        var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase", false);

        if (useInMemory)
        {
            services.AddDbContext<CaseInterviewDbContext>(options =>
            {
                options.UseInMemoryDatabase("CaseInterviewLiveDb");
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=caseinterview_db;Username=postgres;Password=postgres";

            services.AddDbContext<CaseInterviewDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseVector();
                    npgsqlOptions.MigrationsAssembly(typeof(CaseInterviewDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
            });
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<CaseInterviewDbContext>());

        // 2. Security (JWT + Password Hashing + Current User)
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        // 3. Storage & PDF & Text Processing
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IPdfExtractor, PdfPigExtractor>();
        services.AddSingleton<ITextChunker, SemanticTextChunker>();

        // 4. AI Clients (Configurable: OpenAI / Azure OpenAI / Ollama / Mock)
        var aiSection = configuration.GetSection(AiSettings.SectionName);
        services.Configure<AiSettings>(aiSection);
        var aiSettings = aiSection.Get<AiSettings>() ?? new AiSettings();

        if (string.Equals(aiSettings.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, MockEmbeddingGenerator>();
        }
        else
        {
            // OpenAI / Azure OpenAI / Ollama (OpenAI compatible endpoint)
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                var options = new OpenAIClientOptions();
                if (!string.IsNullOrWhiteSpace(aiSettings.Endpoint))
                {
                    options.Endpoint = new Uri(aiSettings.Endpoint);
                }

                var credential = new ApiKeyCredential(aiSettings.ApiKey ?? "not-provided");
                var client = new OpenAIClient(credential, options);
                return client.GetEmbeddingClient(aiSettings.EmbeddingModel).AsIEmbeddingGenerator(aiSettings.EmbeddingDimensions);
            });
        }

        services.AddScoped<IEmbeddingService, EmbeddingService>();

        // 5. Background Processing (Channel<T> + BackgroundService)
        services.AddSingleton<IIngestionQueue, ChannelIngestionQueue>();
        services.AddHostedService<BookIngestionBackgroundService>();

        return services;
    }
}
