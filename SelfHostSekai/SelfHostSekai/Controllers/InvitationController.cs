using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

namespace SelfHostSekai.Controllers;

[Authorize]
[ApiController]
[Route("api/user/{urlUserId:long}/invitation")]
public class InvitationController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(long urlUserId)
    {
        return Ok(new GetUserInvitationResponse
        {
            userInvitations = []
        });
    }
}