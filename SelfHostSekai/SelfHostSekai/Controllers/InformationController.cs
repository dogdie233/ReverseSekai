using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/information")]
public class InformationController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new InformationResponse
        {
            informations = []
        });
    }
}