namespace SelfHostSekai.Configuration;

public class JwtOptions
{
    public string SecretKey { get; set; } = "SecretKeyForJwtAuthentication1234";
    public bool BypassCredValidation { get; set; } = false;
}