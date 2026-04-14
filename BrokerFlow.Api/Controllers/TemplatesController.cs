using System.Xml.Linq;
using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;
    private readonly MappingEngineService _engine;

    public TemplatesController(BrokerFlowDbContext db, MappingEngineService engine)
    {
        _db = db;
        _engine = engine;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _db.XmlTemplates.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(templates);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var template = await _db.XmlTemplates.FindAsync(id);
        return template == null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TemplateDto dto)
    {
        var content = CleanXmlDeclaration(dto.Content ?? "<Document/>");
        var fields = _engine.ExtractXmlFields(content);

        var template = new XmlTemplate
        {
            Name = dto.Name ?? "New Template",
            Content = content,
            FieldsJson = System.Text.Json.JsonSerializer.Serialize(fields)
        };
        _db.XmlTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] TemplateDto dto)
    {
        var template = await _db.XmlTemplates.FindAsync(id);
        if (template == null) return NotFound();

        if (dto.Name != null) template.Name = dto.Name;
        if (dto.Content != null)
        {
            template.Content = CleanXmlDeclaration(dto.Content);
            template.FieldsJson = System.Text.Json.JsonSerializer.Serialize(
                _engine.ExtractXmlFields(template.Content));
        }

        await _db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var template = await _db.XmlTemplates.FindAsync(id);
        if (template == null) return NotFound();
        _db.XmlTemplates.Remove(template);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    [HttpPost("build-from-fields")]
    public IActionResult BuildFromFields([FromBody] BuildFromFieldsDto dto)
    {
        var rootName = dto.RootElement ?? "Document";
        var root = new XElement(rootName);

        foreach (var field in dto.Fields ?? new())
        {
            var parts = field.Path.Split('/');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == parts.Length - 1)
                {
                    current.Add(new XElement(parts[i], field.DefaultValue ?? ""));
                }
                else
                {
                    var child = current.Element(parts[i]);
                    if (child == null)
                    {
                        child = new XElement(parts[i]);
                        current.Add(child);
                    }
                    current = child;
                }
            }
        }

        var content = root.ToString();
        var fields = _engine.ExtractXmlFields(content);

        return Ok(new { content, fields, name = dto.Name ?? "New Template" });
    }

    [HttpPost("extract-fields")]
    public IActionResult ExtractFields([FromBody] ExtractFieldsDto dto)
    {
        if (string.IsNullOrEmpty(dto.XmlContent))
            return BadRequest("No XML content");

        var fields = _engine.ExtractXmlFields(dto.XmlContent);
        return Ok(new { fields });
    }

    private string CleanXmlDeclaration(string xml)
    {
        return System.Text.RegularExpressions.Regex.Replace(xml, @"<\?xml[^?]*\?>", "").Trim();
    }
}
