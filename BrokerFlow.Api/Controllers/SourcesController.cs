using BrokerFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;

    public SourcesController(BrokerFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sources = await _db.Sources.OrderByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(sources);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var source = await _db.Sources.FindAsync(id);
        return source == null ? NotFound() : Ok(source);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SourceDto dto)
    {
        var source = new Source
        {
            Name = dto.Name ?? "New Source",
            Path = dto.Path ?? "",
            FileMask = dto.FileMask ?? "*.*",
            FileFormat = dto.FileFormat ?? "auto",
            CsvSeparator = dto.CsvSeparator,
            CsvCustomSeparator = dto.CsvCustomSeparator,
            Enabled = dto.Enabled
        };
        _db.Sources.Add(source);
        _db.AuditEntries.Add(new AuditEntry { Action = "source_created", EntityType = "Source", EntityId = source.Id, Details = source.Name });
        await _db.SaveChangesAsync();
        return Ok(source);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SourceDto dto)
    {
        var source = await _db.Sources.FindAsync(id);
        if (source == null) return NotFound();

        if (dto.Name != null) source.Name = dto.Name;
        if (dto.Path != null) source.Path = dto.Path;
        if (dto.FileMask != null) source.FileMask = dto.FileMask;
        if (dto.FileFormat != null) source.FileFormat = dto.FileFormat;
        source.CsvSeparator = dto.CsvSeparator;
        source.CsvCustomSeparator = dto.CsvCustomSeparator;
        source.Enabled = dto.Enabled;
        source.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(source);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var source = await _db.Sources.FindAsync(id);
        if (source == null) return NotFound();
        _db.Sources.Remove(source);
        _db.AuditEntries.Add(new AuditEntry { Action = "source_deleted", EntityType = "Source", EntityId = id });
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }
}
