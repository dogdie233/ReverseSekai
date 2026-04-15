using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.Sekai;

namespace SelfHostSekai.Controllers;

/// <summary>
/// GET /api/module-maintenance/{module}
/// The client checks this before entering MultiLive, CheerfulLive, RankMatch, etc.
/// Always returns not-in-maintenance for our private server.
/// </summary>
[ApiController]
[Route("api/module-maintenance")]
public class ModuleMaintenanceController : ControllerBase
{
    [HttpGet("{module}")]
    public IActionResult GetModuleMaintenance(string module)
    {
        return Ok(new
        {
            moduleMaintenanceType = module.ToLowerInvariant() switch
            {
                "multi_live" => "multi_live",
                "cheerful_live" => "cheerful_live",
                "rank_match" => "rank_match",
                "virtual_live" => "virtual_live",
                _ => module
            },
            isOngoing = false
        });
    }
}
