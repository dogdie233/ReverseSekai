namespace SelfHostSekai.Configuration;

public class JwtOptions
{
    public string Issuer { get; set; } = "SekaiServer";
    public string Audience { get; set; } = "SekaiClient";
    public string SecretKey { get; set; } = "SecretKeyForJwtAuthentication1234";
}