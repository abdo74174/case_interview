using System.Security.Claims;
using CaseInterview.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CaseInterview.Infrastructure.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(idClaim, out var userId) ? userId : null;
        }
    }

    public string? UserEmail =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;

    public string? Role =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

    public bool IsAdmin =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
