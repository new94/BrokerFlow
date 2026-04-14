using Newtonsoft.Json.Linq;

namespace BrokerFlow.Api.Models;

// ─── Source DTOs ─────────────────────────────────────────────────────────────

public class SourceDto
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? FileMask { get; set; }
    public string? FileFormat { get; set; }
    public string? CsvSeparator { get; set; }
    public string? CsvCustomSeparator { get; set; }
    public bool Enabled { get; set; } = true;
}

// ─── Template DTOs ───────────────────────────────────────────────────────────

public class TemplateDto
{
    public string? Name { get; set; }
    public string? Content { get; set; }
}

public class BuildFromFieldsDto
{
    public string? Name { get; set; }
    public string? RootElement { get; set; }
    public List<XmlFieldDef>? Fields { get; set; }
}

public class XmlFieldDef
{
    public string Path { get; set; } = "";
    public string? DefaultValue { get; set; }
}

// ─── Mapping DTOs ────────────────────────────────────────────────────────────

public class MappingDto
{
    public string? Name { get; set; }
    public string? SourceId { get; set; }
    public string? TemplateId { get; set; }
    public string? XmlTemplate { get; set; }
    public JArray? Rules { get; set; }
    public bool SplitOutput { get; set; }
    public JObject? SplitCondition { get; set; }
    public string? SplitFileNamePattern { get; set; }
}

// ─── Schedule DTOs ───────────────────────────────────────────────────────────

public class ScheduleDto
{
    public string? Name { get; set; }
    public string? SourceId { get; set; }
    public string? MappingId { get; set; }
    public string? CronExpression { get; set; }
    public bool Enabled { get; set; } = true;
}

// ─── Job DTOs ────────────────────────────────────────────────────────────────

public class CreateJobDto
{
    public string? SourceId { get; set; }
    public string? MappingId { get; set; }
    public string? FilePath { get; set; }
}

// ─── Parse/Preview DTOs ──────────────────────────────────────────────────────

public class ParseRequestDto
{
    public string? FilePath { get; set; }
    public string? FileFormat { get; set; }
    public string? CsvSeparator { get; set; }
}

public class MappingPreviewDto
{
    public JArray? Rules { get; set; }
    public List<Dictionary<string, object?>>? Records { get; set; }
    public string? XmlTemplate { get; set; }
}

public class ExtractFieldsDto
{
    public string? XmlContent { get; set; }
}

// ─── Config DTOs ─────────────────────────────────────────────────────────────

public class ConfigDto
{
    public string? ReportsDir { get; set; }
    public string? OutputDir { get; set; }
    public string? UploadsDir { get; set; }
}

// ─── Expression types (for JSON serialization) ──────────────────────────────

public class ExpressionNode
{
    public string Type { get; set; } = "literal"; // field, literal, arithmetic, string_op, compare, logical, conditional, guid, date_diff, deleted_check
    public string? Name { get; set; }      // for field
    public string? Value { get; set; }     // for literal
    public string? Op { get; set; }        // for arithmetic, string_op, compare, logical
    public JObject? Left { get; set; }     // for arithmetic, compare
    public JObject? Right { get; set; }    // for arithmetic, compare
    public JArray? Operands { get; set; }  // for logical
    public JObject? Condition { get; set; } // for conditional
    public JObject? Then { get; set; }     // for conditional
    public JObject? Else { get; set; }     // for conditional
    public JObject? Source { get; set; }   // for guid (source field)
    public JObject? DateStart { get; set; } // for date_diff
    public JObject? DateEnd { get; set; }  // for date_diff
    public string? Unit { get; set; }      // for date_diff: days, hours, minutes
    public JObject? TrueValue { get; set; }  // for deleted_check
    public JObject? FalseValue { get; set; } // for deleted_check
    public string? CheckField { get; set; }  // for deleted_check
}
