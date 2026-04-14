using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulesController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;
    private readonly SchedulerService _scheduler;

    public SchedulesController(BrokerFlowDbContext db, SchedulerService scheduler)
    {
        _db = db;
        _scheduler = scheduler;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _db.Schedules.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var schedule = await _db.Schedules.FindAsync(id);
        return schedule == null ? NotFound() : Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScheduleDto dto)
    {
        var schedule = new Schedule
        {
            Name = dto.Name ?? "New Schedule",
            SourceId = dto.SourceId,
            MappingId = dto.MappingId,
            CronExpression = dto.CronExpression ?? "0 */5 * * *",
            Enabled = dto.Enabled
        };
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        await _scheduler.SyncSchedulesAsync();
        return Ok(schedule);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ScheduleDto dto)
    {
        var schedule = await _db.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();

        if (dto.Name != null) schedule.Name = dto.Name;
        if (dto.SourceId != null) schedule.SourceId = dto.SourceId;
        if (dto.MappingId != null) schedule.MappingId = dto.MappingId;
        if (dto.CronExpression != null) schedule.CronExpression = dto.CronExpression;
        schedule.Enabled = dto.Enabled;

        await _db.SaveChangesAsync();
        await _scheduler.SyncSchedulesAsync();
        return Ok(schedule);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var schedule = await _db.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();
        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();
        await _scheduler.SyncSchedulesAsync();
        return Ok(new { deleted = true });
    }

    [HttpPost("validate-cron")]
    public IActionResult ValidateCron([FromBody] Dictionary<string, string> body)
    {
        var expr = body.GetValueOrDefault("expression", "");
        try
        {
            var cronExpr = new CronExpression(expr);
            var nextRuns = new List<DateTime>();
            DateTimeOffset? next = DateTimeOffset.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                next = cronExpr.GetNextValidTimeAfter(next.Value);
                if (next.HasValue)
                    nextRuns.Add(next.Value.UtcDateTime);
                else break;
            }
            return Ok(new { valid = true, nextRuns });
        }
        catch
        {
            return Ok(new { valid = false, error = "Invalid cron expression" });
        }
    }
}
