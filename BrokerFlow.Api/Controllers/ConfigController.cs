using BrokerFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;

    public ConfigController(BrokerFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var configs = await _db.AppConfigs.ToListAsync();
        var dict = configs.ToDictionary(c => c.Key, c => c.Value);
        return Ok(dict);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ConfigDto dto)
    {
        async Task SetConfig(string key, string? value)
        {
            if (value == null) return;
            var existing = await _db.AppConfigs.FindAsync(key);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.AppConfigs.Add(new AppConfig { Key = key, Value = value });
            }
        }

        await SetConfig("reports_dir", dto.ReportsDir);
        await SetConfig("output_dir", dto.OutputDir);
        await SetConfig("uploads_dir", dto.UploadsDir);

        _db.AuditEntries.Add(new AuditEntry
        {
            Action = "config_updated",
            EntityType = "AppConfig",
            Details = $"reports={dto.ReportsDir}, output={dto.OutputDir}"
        });

        await _db.SaveChangesAsync();
        return Ok(new { saved = true });
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;

    public AuditController(BrokerFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var query = _db.AuditEntries.OrderByDescending(a => a.CreatedAt);
        var total = await query.CountAsync();
        var entries = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { entries, total, page, pageSize });
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;

    public HealthController(BrokerFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await _db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", database = "connected", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", database = "disconnected", error = ex.Message });
        }
    }
}
