using BrokerFlow.Api.Models;
using BrokerFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly BrokerFlowDbContext _db;
    private readonly FileParserService _parser;
    private readonly IConfiguration _config;

    public FilesController(BrokerFlowDbContext db, FileParserService parser, IConfiguration config)
    {
        _db = db;
        _parser = parser;
        _config = config;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)] // 100MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest("Empty file");

        var uploadsDir = await GetUploadsDir();
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, safeName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        _db.AuditEntries.Add(new AuditEntry
        {
            Action = "file_uploaded",
            EntityType = "File",
            Details = safeName
        });
        await _db.SaveChangesAsync();

        return Ok(new { path = filePath, name = safeName, size = file.Length });
    }

    [HttpGet("scan")]
    public IActionResult Scan([FromQuery] string? path, [FromQuery] string? mask)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return BadRequest("Directory not found");

        var fileMask = mask ?? "*.*";
        var files = Directory.GetFiles(path, fileMask, SearchOption.TopDirectoryOnly)
            .Select(f => new
            {
                path = f,
                name = Path.GetFileName(f),
                size = new FileInfo(f).Length,
                modified = new FileInfo(f).LastWriteTimeUtc
            })
            .OrderByDescending(f => f.modified)
            .Take(200)
            .ToList();

        return Ok(new { files, total = files.Count });
    }

    [HttpPost("parse")]
    public IActionResult Parse([FromBody] ParseRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.FilePath) || !System.IO.File.Exists(dto.FilePath))
            return BadRequest("File not found");

        try
        {
            var (records, fields) = _parser.ParseFile(dto.FilePath, dto.FileFormat, dto.CsvSeparator);
            return Ok(new
            {
                fields,
                records = records.Take(100), // preview limit
                total = records.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("list-outputs")]
    public async Task<IActionResult> ListOutputs()
    {
        var outputDir = await GetOutputDir();
        if (!Directory.Exists(outputDir))
            return Ok(new { files = Array.Empty<object>(), outputDir });

        var files = Directory.GetFiles(outputDir, "*.xml")
            .Select(f => new
            {
                path = f,
                name = Path.GetFileName(f),
                size = new FileInfo(f).Length,
                modified = new FileInfo(f).LastWriteTimeUtc
            })
            .OrderByDescending(f => f.modified)
            .Take(200)
            .ToList();

        return Ok(new { files, outputDir });
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "application/xml", Path.GetFileName(path));
    }

    private async Task<string> GetUploadsDir()
    {
        var config = await _db.AppConfigs.FindAsync("uploads_dir");
        return config?.Value ?? _config["Paths:Uploads"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    }

    private async Task<string> GetOutputDir()
    {
        var config = await _db.AppConfigs.FindAsync("output_dir");
        return config?.Value ?? _config["Paths:Output"] ?? Path.Combine(AppContext.BaseDirectory, "output");
    }
}
