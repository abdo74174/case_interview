using CaseInterview.Domain.Entities;

namespace CaseInterview.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
