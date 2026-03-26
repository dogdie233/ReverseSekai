using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SekaiApiModel.Sekai;
using SelfHostSekai.Constants;
using SelfHostSekai.Data;
using SelfHostSekai.Models;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public UserController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPut("{urlUserId}/auth")]
    public async Task<IActionResult> Auth(string urlUserId, [FromBody] UserAuthRequest request)
    {
        var bypassValidation = _configuration.GetValue<bool>("Auth:BypassJwtValidation", false);
        var jwtKey = _configuration["Auth:JwtKey"] ?? "SecretKeyForJwtAuthentication1234!";

        string userId;
        if (bypassValidation)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(request.credential);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim == null) return Unauthorized();
            userId = userIdClaim.Value;
        }
        else
        {
            var handler = new JwtSecurityTokenHandler();
            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };

                var principal = handler.ValidateToken(request.credential, validationParameters, out var validatedToken);
                var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "userId");
                if (userIdClaim == null) return Unauthorized();
                userId = userIdClaim.Value;
            }
            catch
            {
                return Unauthorized();
            }
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            user = new User { Id = userId };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        var sessionTokenGuid = Guid.NewGuid().ToString();
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                { "sessionToken", sessionTokenGuid },
                { "userId", userId }
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var sessionTokenString = tokenHandler.WriteToken(token);

        var response = new UserAuthResponse
        {
            sessionToken = sessionTokenString,
            appVersion = GameConstants.AppVersion,
            multiPlayVersion = GameConstants.MultiPlayVersion,
            dataVersion = GameConstants.DataVersion,
            assetVersion = GameConstants.AssetVersion,
            removeAssetVersion = GameConstants.RemoveAssetVersion,
            assetHash = GameConstants.AssetHash,
            appVersionStatus = GameConstants.AppVersionStatus,
            isStreamingVirtualLiveForceOpenUser = GameConstants.IsStreamingVirtualLiveForceOpenUser,
            updatedResources = new SuiteUser(), // Cannot be null normally based on sample
            suiteMasterSplitPath = GameConstants.SuiteMasterSplitPath,
            obtainedBondsRewardIds = GameConstants.ObtainedBondsRewardIds
        };

        return Ok(response);
    }
}