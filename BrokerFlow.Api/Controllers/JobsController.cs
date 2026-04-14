using BrokerFlow.Api.Hubs;
using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;
    private readonly JobProcessingService _jobService;
    private readonly IHubContext<JobHub> _hubContext;

    public JobsController(BrokerFlowDbContext db, JobProcessingService jobService, IHubContext<JobHub> hubContext)
    {
        _db = db;
        _jobService = jobService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _db.ProcessingJobs.OrderByDescending(j => j.CreatedAt);
        var total = await query.CountAsync();
        var jobs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { jobs, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var job = await _db.ProcessingJobs.FindAsync(id);
        return job == null ? NotFound() : Ok(job);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobDto dto)
    {
        var job = new ProcessingJob
        {
            SourceId = dto.SourceId,
            MappingId = dto.MappingId,
            FilePath = dto.FilePath,
            OriginalFileName = !string.IsNullOrEmpty(dto.FilePath) ? Path.GetFileName(dto.FilePath) : null,
            Status = "pending"
        };
        _db.ProcessingJobs.Add(job);
        await _db.SaveChangesAsync();

        // Process async
        _ = Task.Run(async () =>
        {
            await _jobService.ProcessJobAsync(job.Id);
            await _hubContext.Clients.All.SendAsync("JobUpdated", job.Id);
        });

        return Ok(job);
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(string id)
    {
        var job = await _db.ProcessingJobs.FindAsync(id);
        if (job == null) return NotFound();

        job.Status = "pending";
        job.ErrorMessage = null;
        job.StartedAt = null;
        job.FinishedAt = null;
        job.RecordsProcessed = 0;
        job.FilesGenerated = 0;
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            await _jobService.ProcessJobAsync(job.Id);
            await _hubContext.Clients.All.SendAsync("JobUpdated", job.Id);
        });

        return Ok(job);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var job = await _db.ProcessingJobs.FindAsync(id);
        if (job == null) return NotFound();
        _db.ProcessingJobs.Remove(job);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }
}
