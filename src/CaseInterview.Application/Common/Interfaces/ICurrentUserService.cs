namespace CaseInterview.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserEmail { get; }
    string? Role { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
}
