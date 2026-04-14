using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace BrokerFlow.Api.Services;

public class MappingEngineService
{
    // ─── Expression Evaluator ────────────────────────────────────────────────

    public object? EvaluateExpression(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? previousRecord = null)
    {
        var type = expr["type"]?.ToString() ?? "literal";

        return type switch
        {
            "field" => GetField(record, expr["name"]?.ToString() ?? ""),
            "literal" => expr["value"]?.ToString() ?? "",
            "arithmetic" => EvalArithmetic(expr, record, previousRecord),
            "string_op" => EvalStringOp(expr, record, previousRecord),
            "compare" => EvalCompare(expr, record, previousRecord),
            "logical" => EvalLogical(expr, record, previousRecord),
            "conditional" => EvalConditional(expr, record, previousRecord),
            "guid" => EvalGuid(expr, record, previousRecord),
            "date_diff" => EvalDateDiff(expr, record, previousRecord),
            "date_sum" => EvalDateSum(expr, record, previousRecord),
            "deleted_check" => EvalDeletedCheck(expr, record, previousRecord),
            _ => ""
        };
    }

    private object? GetField(Dictionary<string, object?> record, string fieldName)
    {
        return record.TryGetValue(fieldName, out var val) ? val : "";
    }

    private double ToNum(object? val)
    {
        if (val == null) return 0;
        var s = val.ToString()?.Replace(",", ".").Trim() ?? "0";
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private string ToStr(object? val) => val?.ToString() ?? "";

    // ── Arithmetic ──────────────────────────────────────────────────────────

    private object? EvalArithmetic(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var left = ToNum(EvaluateExpression(expr["left"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var right = ToNum(EvaluateExpression(expr["right"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var op = expr["op"]?.ToString() ?? "+";

        double result = op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right != 0 ? left / right : 0,
            "%" => right != 0 ? left % right : 0,
            _ => 0
        };

        return result == Math.Floor(result) ? ((long)result).ToString() : result.ToString(CultureInfo.InvariantCulture);
    }

    // ── String operations ───────────────────────────────────────────────────

    private object? EvalStringOp(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var op = expr["op"]?.ToString() ?? "";
        var val = ToStr(EvaluateExpression(expr["value"]?.ToObject<JObject>() ?? new JObject(), record, prev));

        return op switch
        {
            "trim" => val.Trim(),
            "upper" => val.ToUpperInvariant(),
            "lower" => val.ToLowerInvariant(),
            "replace" => val.Replace(
                ToStr(EvaluateExpression(expr["search"]?.ToObject<JObject>() ?? new JObject(), record, prev)),
                ToStr(EvaluateExpression(expr["replacement"]?.ToObject<JObject>() ?? new JObject(), record, prev))),
            "substr" => EvalSubstr(val, expr, record, prev),
            "concat" => EvalConcat(expr, record, prev),
            "regex" => EvalRegex(val, expr),
            "length" => val.Length.ToString(),
            "left" => val.Length >= (int)ToNum(expr["count"]?.ToObject<JObject>() ?? new JObject())
                ? val[..(int)ToNum(expr["count"]?.ToObject<JObject>() ?? new JObject())]
                : val,
            "right" => val.Length >= (int)ToNum(expr["count"]?.ToObject<JObject>() ?? new JObject())
                ? val[^(int)ToNum(expr["count"]?.ToObject<JObject>() ?? new JObject())..]
                : val,
            _ => val
        };
    }

    private string EvalSubstr(string val, JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var start = (int)ToNum(EvaluateExpression(expr["start"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var length = expr["length"] != null
            ? (int)ToNum(EvaluateExpression(expr["length"]?.ToObject<JObject>() ?? new JObject(), record, prev))
            : val.Length - start;

        if (start < 0) start = 0;
        if (start >= val.Length) return "";
        length = Math.Min(length, val.Length - start);
        return val.Substring(start, length);
    }

    private string EvalConcat(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var parts = expr["parts"]?.ToObject<JArray>() ?? new JArray();
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(ToStr(EvaluateExpression(part.ToObject<JObject>() ?? new JObject(), record, prev)));
        }
        return sb.ToString();
    }

    private string EvalRegex(string val, JObject expr)
    {
        var pattern = expr["pattern"]?.ToString() ?? "";
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(val, pattern);
            return match.Success ? match.Value : "";
        }
        catch
        {
            return "";
        }
    }

    // ── Comparison ──────────────────────────────────────────────────────────

    private object? EvalCompare(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var left = ToStr(EvaluateExpression(expr["left"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var right = ToStr(EvaluateExpression(expr["right"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var op = expr["op"]?.ToString() ?? "==";

        return op switch
        {
            "==" => left == right,
            "!=" => left != right,
            ">" => ToNum(left) > ToNum(right),
            "<" => ToNum(left) < ToNum(right),
            ">=" => ToNum(left) >= ToNum(right),
            "<=" => ToNum(left) <= ToNum(right),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "not_contains" => !left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "startswith" => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            "endswith" => left.EndsWith(right, StringComparison.OrdinalIgnoreCase),
            "like" => EvalLike(left, right),
            "not_like" => !EvalLike(left, right),
            "is_empty" => string.IsNullOrWhiteSpace(left),
            "is_not_empty" => !string.IsNullOrWhiteSpace(left),
            _ => false
        };
    }

    private bool EvalLike(string val, string pattern)
    {
        // SQL LIKE: % = any chars, _ = single char
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("%", ".*").Replace("_", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(val, regex, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // ── Logical ─────────────────────────────────────────────────────────────

    private object? EvalLogical(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var op = expr["op"]?.ToString() ?? "and";
        var operands = expr["operands"]?.ToObject<JArray>() ?? new JArray();

        return op switch
        {
            "and" => operands.All(o => ToBool(EvaluateExpression(o.ToObject<JObject>() ?? new JObject(), record, prev))),
            "or" => operands.Any(o => ToBool(EvaluateExpression(o.ToObject<JObject>() ?? new JObject(), record, prev))),
            "not" => operands.Count > 0 && !ToBool(EvaluateExpression(operands[0].ToObject<JObject>() ?? new JObject(), record, prev)),
            _ => false
        };
    }

    private bool ToBool(object? val)
    {
        if (val == null) return false;
        if (val is bool b) return b;
        var s = val.ToString()?.ToLowerInvariant() ?? "";
        return s != "" && s != "0" && s != "false";
    }

    // ── Conditional ─────────────────────────────────────────────────────────

    private object? EvalConditional(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var cond = ToBool(EvaluateExpression(expr["condition"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var branch = cond
            ? expr["then"]?.ToObject<JObject>() ?? new JObject()
            : expr["else"]?.ToObject<JObject>() ?? new JObject { ["type"] = "literal", ["value"] = "" };
        return EvaluateExpression(branch, record, prev);
    }

    // ── GUID from field ─────────────────────────────────────────────────────

    private object? EvalGuid(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var sourceExpr = expr["source"]?.ToObject<JObject>();
        if (sourceExpr == null) return Guid.NewGuid().ToString();

        var sourceVal = ToStr(EvaluateExpression(sourceExpr, record, prev));
        // Deterministic GUID based on field value (MD5-based UUID v3 style)
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sourceVal));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant
        return new Guid(hash).ToString();
    }

    // ── Date diff ───────────────────────────────────────────────────────────

    private object? EvalDateDiff(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var startStr = ToStr(EvaluateExpression(expr["date_start"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var endStr = ToStr(EvaluateExpression(expr["date_end"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var unit = expr["unit"]?.ToString() ?? "days";

        if (!DateTime.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
            return "0";
        if (!DateTime.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            return "0";

        var diff = endDate - startDate;
        return unit switch
        {
            "days" => ((long)diff.TotalDays).ToString(),
            "hours" => ((long)diff.TotalHours).ToString(),
            "minutes" => ((long)diff.TotalMinutes).ToString(),
            "seconds" => ((long)diff.TotalSeconds).ToString(),
            _ => ((long)diff.TotalDays).ToString()
        };
    }

    // ── Date sum (add/subtract days from date) ──────────────────────────────

    private object? EvalDateSum(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var dateStr = ToStr(EvaluateExpression(expr["date"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var amount = ToNum(EvaluateExpression(expr["amount"]?.ToObject<JObject>() ?? new JObject(), record, prev));
        var unit = expr["unit"]?.ToString() ?? "days";
        var op = expr["op"]?.ToString() ?? "+";

        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return dateStr;

        if (op == "-") amount = -amount;

        var result = unit switch
        {
            "days" => date.AddDays(amount),
            "hours" => date.AddHours(amount),
            "minutes" => date.AddMinutes(amount),
            "months" => date.AddMonths((int)amount),
            "years" => date.AddYears((int)amount),
            _ => date.AddDays(amount)
        };

        return result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    // ── Deleted in previous report check ────────────────────────────────────

    private object? EvalDeletedCheck(JObject expr, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev)
    {
        var checkField = expr["check_field"]?.ToString() ?? "";
        var trueExpr = expr["true_value"]?.ToObject<JObject>() ?? new JObject { ["type"] = "literal", ["value"] = "true" };
        var falseExpr = expr["false_value"]?.ToObject<JObject>() ?? new JObject { ["type"] = "literal", ["value"] = "false" };

        // If previous record exists and had this field but current doesn't, it's "deleted"
        bool isDeleted = false;
        if (prev != null && prev.ContainsKey(checkField))
        {
            var prevVal = ToStr(prev[checkField]);
            var curVal = record.ContainsKey(checkField) ? ToStr(record[checkField]) : null;
            isDeleted = curVal == null || string.IsNullOrEmpty(curVal);
        }

        return EvaluateExpression(isDeleted ? trueExpr : falseExpr, record, prev);
    }

    // ─── Condition Evaluator ─────────────────────────────────────────────────

    public bool EvaluateCondition(JObject? condition, Dictionary<string, object?> record,
        Dictionary<string, object?>? prev = null)
    {
        if (condition == null) return true;
        return ToBool(EvaluateExpression(condition, record, prev));
    }

    // ─── XML Builder ─────────────────────────────────────────────────────────

    public XElement BuildXmlDocument(JArray rules, Dictionary<string, object?> record,
        string? xmlTemplateStr = null, Dictionary<string, object?>? prev = null)
    {
        XElement root;

        if (!string.IsNullOrEmpty(xmlTemplateStr))
        {
            // Clean XML declaration
            var clean = System.Text.RegularExpressions.Regex.Replace(
                xmlTemplateStr, @"<\?xml[^?]*\?>", "").Trim();
            try
            {
                root = XElement.Parse(clean);
            }
            catch
            {
                root = new XElement("Document");
            }
        }
        else
        {
            root = new XElement("Document");
        }

        foreach (var ruleToken in rules)
        {
            var rule = ruleToken as JObject;
            if (rule == null) continue;

            var condition = rule["condition"]?.ToObject<JObject>();
            if (!EvaluateCondition(condition, record, prev))
                continue;

            var xmlPath = rule["xml_path"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(xmlPath)) continue;

            var expression = rule["expression"]?.ToObject<JObject>() ?? new JObject { ["type"] = "literal", ["value"] = "" };
            var value = ToStr(EvaluateExpression(expression, record, prev));

            SetXmlValue(root, xmlPath, value);
        }

        return root;
    }

    private void SetXmlValue(XElement root, string path, string value)
    {
        var parts = path.Split('/');
        var current = root;

        for (int i = 0; i < parts.Length; i++)
        {
            if (i == parts.Length - 1)
            {
                // Check if it's an attribute
                if (parts[i].StartsWith("@"))
                {
                    current.SetAttributeValue(parts[i][1..], value);
                }
                else
                {
                    var existing = current.Element(parts[i]);
                    if (existing != null)
                        existing.Value = value;
                    else
                        current.Add(new XElement(parts[i], value));
                }
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

    // ─── Apply mapping to all records ────────────────────────────────────────

    public List<string> ApplyMapping(JArray rules, List<Dictionary<string, object?>> records,
        string? xmlTemplate = null, bool splitOutput = false, JObject? splitCondition = null,
        string? splitFileNamePattern = null)
    {
        var results = new List<string>();
        Dictionary<string, object?>? prevRecord = null;

        if (splitOutput)
        {
            // Generate one XML per matching record
            foreach (var record in records)
            {
                if (splitCondition != null && !EvaluateCondition(splitCondition, record, prevRecord))
                {
                    prevRecord = record;
                    continue;
                }

                var doc = BuildXmlDocument(rules, record, xmlTemplate, prevRecord);
                results.Add(FormatXml(doc));
                prevRecord = record;
            }
        }
        else
        {
            // Single output with all records as repeated elements
            XElement? root = null;
            string repeatingElementName = "Record";

            if (!string.IsNullOrEmpty(xmlTemplate))
            {
                var clean = System.Text.RegularExpressions.Regex.Replace(
                    xmlTemplate, @"<\?xml[^?]*\?>", "").Trim();
                try
                {
                    root = XElement.Parse(clean);
                    repeatingElementName = root.Elements().FirstOrDefault()?.Name.LocalName ?? "Record";
                    root.RemoveAll();
                }
                catch
                {
                    root = new XElement("Document");
                }
            }
            else
            {
                root = new XElement("Document");
            }

            foreach (var record in records)
            {
                var recordElement = BuildXmlDocument(rules, record, null, prevRecord);
                recordElement.Name = repeatingElementName;
                root.Add(recordElement);
                prevRecord = record;
            }

            results.Add(FormatXml(root));
        }

        return results;
    }

    public string ResolveSplitFileName(string pattern, Dictionary<string, object?> record, int index)
    {
        var name = pattern;
        // Replace {FieldName} with actual values
        foreach (var kvp in record)
        {
            name = name.Replace($"{{{kvp.Key}}}", ToStr(kvp.Value));
        }
        name = name.Replace("{_index}", index.ToString());
        name = name.Replace("{_date}", DateTime.Now.ToString("yyyyMMdd"));
        name = name.Replace("{_timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        // Sanitize filename
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            name += ".xml";

        return name;
    }

    // ─── XML formatting ──────────────────────────────────────────────────────

    private string FormatXml(XElement element)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{element.ToString()}";
    }

    // ─── Extract fields from XML template ────────────────────────────────────

    public List<string> ExtractXmlFields(string xmlContent)
    {
        var fields = new List<string>();
        var clean = System.Text.RegularExpressions.Regex.Replace(
            xmlContent, @"<\?xml[^?]*\?>", "").Trim();

        try
        {
            var root = XElement.Parse(clean);
            ExtractPaths(root, "", fields);
        }
        catch
        {
            // ignore
        }

        return fields;
    }

    private void ExtractPaths(XElement el, string prefix, List<string> fields)
    {
        foreach (var attr in el.Attributes())
        {
            fields.Add(string.IsNullOrEmpty(prefix) ? $"@{attr.Name.LocalName}" : $"{prefix}/@{attr.Name.LocalName}");
        }

        var children = el.Elements().ToList();
        if (children.Count == 0)
        {
            if (!string.IsNullOrEmpty(prefix))
                fields.Add(prefix);
        }
        else
        {
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(prefix)
                    ? child.Name.LocalName
                    : $"{prefix}/{child.Name.LocalName}";
                ExtractPaths(child, childPath, fields);
            }
        }
    }
}
