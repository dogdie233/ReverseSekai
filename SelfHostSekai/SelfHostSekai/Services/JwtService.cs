using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SelfHostSekai.Configuration;

namespace SelfHostSekai.Services;

public class JwtService
{
    private readonly JwtOptions _jwtOptions;
    private readonly byte[] _secretKeyBytes;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
        _secretKeyBytes = Encoding.UTF8.GetBytes(_jwtOptions.SecretKey);
    }

    public long? ValidateAndGetUserIdFromCredential(string credential)
    {
        var bypassValidation = _jwtOptions.BypassCredValidation;
        var handler = new JwtSecurityTokenHandler();

        long userId;
        if (bypassValidation)
        {
            var jwtToken = handler.ReadJwtToken(credential);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out userId)) return null;
            return userId;
        }

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_secretKeyBytes),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false
            };

            var principal = handler.ValidateToken(credential, validationParameters, out _);
            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out userId)) return null;
            return userId;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateSessionToken(long userId, string sessionTokenGuid)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                { "sessionToken", sessionTokenGuid },
                { "userId", userId.ToString() }
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_secretKeyBytes), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateUserIdToken(long userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                { "userId", userId.ToString() }
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_secretKeyBytes), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateCredToken(long userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                { "credential", Guid.NewGuid().ToString() },
                { "userId", userId.ToString() }
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_secretKeyBytes), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}