using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SelfHostSekai.Controllers;

[Authorize]
[ApiController]
[Route("api/user/{urlUserId:long}/present")]
public class PresentController : ControllerBase
{

}