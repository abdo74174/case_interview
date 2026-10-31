using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Features.Auth.DTOs;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CaseInterview.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Email", new[] { "An account with this email address already exists." } }
            });
        }

        var user = new User
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.GenerateToken(user);
        return new AuthResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(24)
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new DomainException("Invalid email or password.");
        }

        var token = _jwtTokenService.GenerateToken(user);
        return new AuthResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(24)
        );
    }
}
