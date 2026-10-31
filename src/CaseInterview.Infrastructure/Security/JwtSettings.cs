namespace CaseInterview.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = "CaseInterview_Super_Secret_Key_For_Development_Must_Be_Long_Enough_2026";
    public string Issuer { get; set; } = "CaseInterviewApi";
    public string Audience { get; set; } = "CaseInterviewUsers";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours
}
