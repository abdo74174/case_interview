namespace CaseInterview.Application.Features.Auth.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? Role = "User"
);

public sealed record LoginRequest(
    string Email,
    string Password
);

public sealed record AuthResponse(
    string Token,
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    DateTime ExpiresAtUtc
);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAtUtc,
    int BooksCount,
    int SessionsCount
);
