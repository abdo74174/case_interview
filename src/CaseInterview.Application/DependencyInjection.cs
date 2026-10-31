using CaseInterview.Application.Features.Auth.Services;
using CaseInterview.Application.Features.Books.Services;
using CaseInterview.Application.Features.Search.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CaseInterview.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Register all FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ISearchService, SearchService>();

        return services;
    }
}
