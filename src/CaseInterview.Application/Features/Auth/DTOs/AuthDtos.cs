namespace CaseInterview.Application.Features.Auth.DTOs;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName
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
    DateTime ExpiresAtUtc
);
