using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MappingsController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;
    private readonly MappingEngineService _engine;

    public MappingsController(BrokerFlowDbContext db, MappingEngineService engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var mappings = await _db.MappingConfigs.OrderByDescending(m => m.CreatedAt).ToListAsync();
        return Ok(mappings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var mapping = await _db.MappingConfigs.FindAsync(id);
        return mapping == null ? NotFound() : Ok(mapping);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MappingDto dto)
    {
        var mapping = new MappingConfig
        {
            Name = dto.Name ?? "New Mapping",
            SourceId = dto.SourceId,
            TemplateId = dto.TemplateId,
            XmlTemplate = dto.XmlTemplate,
            RulesJson = dto.Rules?.ToString() ?? "[]",
            SplitOutput = dto.SplitOutput,
            SplitConditionJson = dto.SplitCondition?.ToString(),
            SplitFileNamePattern = dto.SplitFileNamePattern
        };
        _db.MappingConfigs.Add(mapping);
        _db.AuditEntries.Add(new AuditEntry { Action = "mapping_created", EntityType = "MappingConfig", EntityId = mapping.Id, Details = mapping.Name });
        await _db.SaveChangesAsync();
        return Ok(mapping);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] MappingDto dto)
    {
        var mapping = await _db.MappingConfigs.FindAsync(id);
        if (mapping == null) return NotFound();

        if (dto.Name != null) mapping.Name = dto.Name;
        if (dto.SourceId != null) mapping.SourceId = dto.SourceId;
        if (dto.TemplateId != null) mapping.TemplateId = dto.TemplateId;
        if (dto.XmlTemplate != null) mapping.XmlTemplate = dto.XmlTemplate;
        if (dto.Rules != null) mapping.RulesJson = dto.Rules.ToString();
        mapping.SplitOutput = dto.SplitOutput;
        mapping.SplitConditionJson = dto.SplitCondition?.ToString();
        mapping.SplitFileNamePattern = dto.SplitFileNamePattern;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(mapping);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var mapping = await _db.MappingConfigs.FindAsync(id);
        if (mapping == null) return NotFound();
        _db.MappingConfigs.Remove(mapping);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] MappingPreviewDto dto)
    {
        try
        {
            var rules = dto.Rules ?? new JArray();
            var records = dto.Records ?? new List<Dictionary<string, object?>>();
            var xmlDocs = _engine.ApplyMapping(rules, records, dto.XmlTemplate);
            return Ok(new { results = xmlDocs, count = xmlDocs.Count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
