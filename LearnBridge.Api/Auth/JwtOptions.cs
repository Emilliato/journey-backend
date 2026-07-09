namespace LearnBridge.Api.Auth;

public sealed class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryDays { get; set; } = 60;
}
