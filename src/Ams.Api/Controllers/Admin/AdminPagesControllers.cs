using Ams.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ams.Application.Common.Dtos;

namespace Ams.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/business-rules")]
[Authorize]
public class AdminBusinessRulesController : ControllerBase
{
    private readonly AdminPagesService _service;
    private readonly ILogger<AdminBusinessRulesController> _logger;

    public AdminBusinessRulesController(AdminPagesService service, ILogger<AdminBusinessRulesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessRuleDto>>> GetRulesAsync(
        [FromQuery] Guid tenantId,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rules = await _service.GetRulesAsync(tenantId, category, cancellationToken);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business rules");
            return StatusCode(500, new { error = "Failed to retrieve business rules" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BusinessRuleDto>> GetRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _service.GetRuleByIdAsync(id, cancellationToken);
            if (rule == null)
                return NotFound();

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business rule {RuleId}", id);
            return StatusCode(500, new { error = "Failed to retrieve business rule" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateRuleAsync(
        [FromBody] BusinessRuleDto rule,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            return BadRequest(new { error = "Rule name is required" });

        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var ruleWithTenant = rule with { TenantId = tenantId };
            var id = await _service.CreateRuleAsync(ruleWithTenant, userId, cancellationToken);

            _logger.LogInformation("Created business rule {RuleId} in tenant {TenantId}", id, tenantId);
            return CreatedAtAction(nameof(GetRuleByIdAsync), new { id }, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business rule");
            return StatusCode(500, new { error = "Failed to create business rule" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRuleAsync(
        Guid id,
        [FromBody] BusinessRuleDto rule,
        CancellationToken cancellationToken = default)
    {
        if (id != rule.BusinessRuleId)
            return BadRequest(new { error = "ID mismatch" });

        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            await _service.UpdateRuleAsync(rule, userId, cancellationToken);

            _logger.LogInformation("Updated business rule {RuleId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating business rule {RuleId}", id);
            return StatusCode(500, new { error = "Failed to update business rule" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRuleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _service.DeleteRuleAsync(id, cancellationToken);

            _logger.LogInformation("Deleted business rule {RuleId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting business rule {RuleId}", id);
            return StatusCode(500, new { error = "Failed to delete business rule" });
        }
    }

    [HttpPatch("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _service.ToggleRuleStatusAsync(id, cancellationToken);

            _logger.LogInformation("Toggled business rule status {RuleId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling business rule status {RuleId}", id);
            return StatusCode(500, new { error = "Failed to toggle rule status" });
        }
    }
}

[ApiController]
[Route("api/admin/teams")]
[Authorize]
public class AdminTeamsController : ControllerBase
{
    private readonly AdminPagesService _service;
    private readonly ILogger<AdminTeamsController> _logger;

    public AdminTeamsController(AdminPagesService service, ILogger<AdminTeamsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentTeamDto>>> GetTeamsAsync(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var teams = await _service.GetTeamsAsync(tenantId, cancellationToken);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving teams");
            return StatusCode(500, new { error = "Failed to retrieve teams" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateTeamAsync(
        [FromBody] DepartmentTeamDto team,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var id = await _service.CreateTeamAsync(team with { TenantId = tenantId }, userId, cancellationToken);
            return CreatedAtAction(nameof(GetTeamAsync), new { id }, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team");
            return StatusCode(500, new { error = "Failed to create team" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentTeamDto>> GetTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await _service.GetTeamByIdAsync(id, cancellationToken);
        return team == null ? NotFound() : Ok(team);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTeamAsync(Guid id, [FromBody] DepartmentTeamDto team, CancellationToken cancellationToken = default)
    {
        if (id != team.TeamId) return BadRequest(new { error = "ID mismatch" });
        await _service.UpdateTeamAsync(team, Guid.Parse(User.FindFirst("sub")?.Value ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteTeamAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin/departments")]
[Authorize]
public class AdminDepartmentsController : ControllerBase
{
    private readonly AdminPagesService _service;
    private readonly ILogger<AdminDepartmentsController> _logger;

    public AdminDepartmentsController(AdminPagesService service, ILogger<AdminDepartmentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartmentsAsync(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var departments = await _service.GetDepartmentsAsync(tenantId, cancellationToken);
            return Ok(departments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500, new { error = "Failed to retrieve departments" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateDepartmentAsync(
        [FromBody] DepartmentDto department,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
            var id = await _service.CreateDepartmentAsync(department with { TenantId = tenantId }, userId, cancellationToken);
            return CreatedAtAction(nameof(GetDepartmentAsync), new { id }, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating department");
            return StatusCode(500, new { error = "Failed to create department" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _service.GetDepartmentByIdAsync(id, cancellationToken);
        return department == null ? NotFound() : Ok(department);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDepartmentAsync(Guid id, [FromBody] DepartmentDto department, CancellationToken cancellationToken = default)
    {
        if (id != department.DepartmentId) return BadRequest(new { error = "ID mismatch" });
        await _service.UpdateDepartmentAsync(department, Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString()), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteDepartmentAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin/staff")]
[Authorize]
public class AdminStaffController : ControllerBase
{
    private readonly AdminPagesService _service;
    private readonly ILogger<AdminStaffController> _logger;

    public AdminStaffController(AdminPagesService service, ILogger<AdminStaffController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProducerStaffDto>>> GetStaffAsync(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var staff = await _service.GetStaffAsync(tenantId, cancellationToken);
        return Ok(staff);
    }

    [HttpGet("expiring-licenses")]
    public async Task<ActionResult<IReadOnlyList<ProducerStaffDto>>> GetExpiringLicensesAsync(
        [FromQuery] Guid tenantId,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var staff = await _service.GetExpiringLicensesAsync(tenantId, days, cancellationToken);
        return Ok(staff);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateStaffAsync(
        [FromBody] ProducerStaffDto staff,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? "");
        var id = await _service.CreateStaffAsync(staff with { TenantId = tenantId }, userId, cancellationToken);
        return CreatedAtAction(nameof(GetStaffMemberAsync), new { id }, id);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProducerStaffDto>> GetStaffMemberAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var staff = await _service.GetStaffByIdAsync(id, cancellationToken);
        return staff == null ? NotFound() : Ok(staff);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStaffAsync(Guid id, [FromBody] ProducerStaffDto staff, CancellationToken cancellationToken = default)
    {
        if (id != staff.StaffId) return BadRequest(new { error = "ID mismatch" });
        await _service.UpdateStaffAsync(staff, Guid.Parse(User.FindFirst("sub")?.Value ?? ""), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteStaffAsync(id, cancellationToken);
        return NoContent();
    }
}
