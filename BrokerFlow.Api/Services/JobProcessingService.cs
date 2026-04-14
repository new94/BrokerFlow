using BrokerFlow.Api.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace BrokerFlow.Api.Services;

public class JobProcessingService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<JobProcessingService> _logger;

    public JobProcessingService(IServiceProvider services, ILogger<JobProcessingService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task ProcessJobAsync(string jobId)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerFlowDbContext>();
        var parser = scope.ServiceProvider.GetRequiredService<FileParserService>();
        var engine = scope.ServiceProvider.GetRequiredService<MappingEngineService>();

        var job = await db.ProcessingJobs.FindAsync(jobId);
        if (job == null) return;

        job.Status = "running";
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        try
        {
            // Load mapping
            var mapping = await db.MappingConfigs.FindAsync(job.MappingId);
            if (mapping == null)
                throw new InvalidOperationException("Mapping configuration not found");

            // Resolve file path
            var filePath = job.FilePath;
            if (string.IsNullOrEmpty(filePath))
                throw new InvalidOperationException("No file path specified");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            // Determine CSV separator
            string? csvSep = null;
            if (!string.IsNullOrEmpty(job.SourceId))
            {
                var source = await db.Sources.FindAsync(job.SourceId);
                csvSep = source?.CsvSeparator == "custom" ? source.CsvCustomSeparator : source?.CsvSeparator;
            }

            // Parse
            var (records, fields) = parser.ParseFile(filePath, null, csvSep);
            
            var rules = JArray.Parse(mapping.RulesJson ?? "[]");
            var xmlTemplate = mapping.XmlTemplate;

            // Load template content if referenced
            if (!string.IsNullOrEmpty(mapping.TemplateId))
            {
                var template = await db.XmlTemplates.FindAsync(mapping.TemplateId);
                if (template != null)
                    xmlTemplate = template.Content;
            }

            var splitOutput = mapping.SplitOutput;
            var splitCondition = !string.IsNullOrEmpty(mapping.SplitConditionJson)
                ? JObject.Parse(mapping.SplitConditionJson)
                : null;
            var splitPattern = mapping.SplitFileNamePattern ?? "output_{_index}_{_date}.xml";

            // Get output directory
            var outputDir = await GetOutputDir(db);
            Directory.CreateDirectory(outputDir);

            if (splitOutput)
            {
                // Generate one file per matching record
                var xmlDocs = engine.ApplyMapping(rules, records, xmlTemplate, true, splitCondition);
                int fileIdx = 0;
                var generatedFiles = new List<string>();

                for (int i = 0; i < xmlDocs.Count; i++)
                {
                    var fileName = engine.ResolveSplitFileName(splitPattern, records[i], fileIdx);
                    var outputPath = Path.Combine(outputDir, fileName);
                    await File.WriteAllTextAsync(outputPath, xmlDocs[i], System.Text.Encoding.UTF8);
                    generatedFiles.Add(outputPath);
                    fileIdx++;
                }

                job.RecordsProcessed = records.Count;
                job.FilesGenerated = generatedFiles.Count;
                job.ResultPath = string.Join(";", generatedFiles);
            }
            else
            {
                // Single output file
                var xmlDocs = engine.ApplyMapping(rules, records, xmlTemplate, false);
                var outputFileName = $"output_{job.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
                var outputPath = Path.Combine(outputDir, outputFileName);
                await File.WriteAllTextAsync(outputPath, xmlDocs.FirstOrDefault() ?? "<Document/>",
                    System.Text.Encoding.UTF8);

                job.RecordsProcessed = records.Count;
                job.FilesGenerated = 1;
                job.ResultPath = outputPath;
            }

            job.Status = "done";
            job.FinishedAt = DateTime.UtcNow;

            // Audit log
            db.AuditEntries.Add(new AuditEntry
            {
                Action = "job_completed",
                EntityType = "ProcessingJob",
                EntityId = job.Id,
                Details = $"Processed {job.RecordsProcessed} records, generated {job.FilesGenerated} files"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Status = "error";
            job.ErrorMessage = ex.Message;
            job.FinishedAt = DateTime.UtcNow;

            db.AuditEntries.Add(new AuditEntry
            {
                Action = "job_failed",
                EntityType = "ProcessingJob",
                EntityId = job.Id,
                Details = ex.Message
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<string> GetOutputDir(BrokerFlowDbContext db)
    {
        var config = await db.AppConfigs.FindAsync("output_dir");
        return config?.Value ?? Path.Combine(AppContext.BaseDirectory, "output");
    }
}
