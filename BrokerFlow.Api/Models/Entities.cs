using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BrokerFlow.Api.Models;

// ─── Source (broker report folder configuration) ─────────────────────────────

public class Source
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    
    [MaxLength(500)]
    public string Path { get; set; } = "";
    
    [MaxLength(200)]
    public string FileMask { get; set; } = "*.*";
    
    [MaxLength(20)]
    public string FileFormat { get; set; } = "auto"; // auto, xml, csv, xls, xlsx, pdf
    
    [MaxLength(10)]
    public string? CsvSeparator { get; set; } // comma, semicolon, tab, pipe, custom
    
    [MaxLength(10)]
    public string? CsvCustomSeparator { get; set; }
    
    public bool Enabled { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ─── XML Template ────────────────────────────────────────────────────────────

public class XmlTemplate
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    
    [Column(TypeName = "nvarchar(max)")]
    public string Content { get; set; } = "";
    
    [Column(TypeName = "nvarchar(max)")]
    public string? FieldsJson { get; set; } // JSON array of extracted field paths
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ─── Mapping Configuration ───────────────────────────────────────────────────

public class MappingConfig
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    
    public string? SourceId { get; set; }
    
    public string? TemplateId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string RulesJson { get; set; } = "[]"; // JSON array of mapping rules
    
    [Column(TypeName = "nvarchar(max)")]
    public string? XmlTemplate { get; set; } // inline XML template
    
    // Per-row output file splitting
    public bool SplitOutput { get; set; } = false;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? SplitConditionJson { get; set; } // JSON condition for splitting
    
    [MaxLength(500)]
    public string? SplitFileNamePattern { get; set; } // e.g., "trade_{TradeId}_{Date}.xml"
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ─── Schedule ────────────────────────────────────────────────────────────────

public class Schedule
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    
    public string? SourceId { get; set; }
    
    public string? MappingId { get; set; }
    
    [MaxLength(100)]
    public string CronExpression { get; set; } = "0 */5 * * *"; // every 5 minutes
    
    public bool Enabled { get; set; } = true;
    
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ─── Processing Job ──────────────────────────────────────────────────────────

public class ProcessingJob
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    public string? SourceId { get; set; }
    public string? MappingId { get; set; }
    
    [MaxLength(500)]
    public string? FilePath { get; set; }
    
    [MaxLength(500)]
    public string? OriginalFileName { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "pending"; // pending, running, done, error
    
    [Column(TypeName = "nvarchar(max)")]
    public string? ResultPath { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }
    
    public int RecordsProcessed { get; set; } = 0;
    public int FilesGenerated { get; set; } = 0;
    
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ─── Audit Log ───────────────────────────────────────────────────────────────

public class AuditEntry
{
    [Key]
    public long Id { get; set; }
    
    [MaxLength(50)]
    public string Action { get; set; } = "";
    
    [MaxLength(200)]
    public string? EntityType { get; set; }
    
    [MaxLength(100)]
    public string? EntityId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Details { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ─── App Config (key-value settings) ─────────────────────────────────────────

public class AppConfig
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = "";
    
    [Column(TypeName = "nvarchar(max)")]
    public string Value { get; set; } = "";
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
