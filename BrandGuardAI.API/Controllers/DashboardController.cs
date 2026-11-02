using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BrandGuardAI.API.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats([FromQuery] int? days, CancellationToken cancellationToken)
    {
        var sanitizedDays = days.HasValue ? Math.Clamp(days.Value, 1, 365) : (int?)null;
        var stats = await _dashboardService.GetDashboardStatsAsync(sanitizedDays, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("intelligence")]
    public async Task<ActionResult<GovernanceIntelligenceDto>> GetIntelligence([FromQuery] int? days = 30, CancellationToken cancellationToken = default)
    {
        int effectiveDays = days.HasValue && days.Value > 0 ? Math.Min(days.Value, 365) : 30;
        var intelligence = await _dashboardService.GetGovernanceIntelligenceAsync(effectiveDays, cancellationToken);
        return Ok(intelligence);
    }
}
