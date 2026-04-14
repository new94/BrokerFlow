using System.Data;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace BrokerFlow.Api.Services;

public class FileParserService
{
    public (List<Dictionary<string, object?>> Records, List<string> Fields) ParseFile(
        string filePath, string? format = null, string? csvSeparator = null)
    {
        var fmt = format?.ToLowerInvariant() ?? DetectFormat(filePath);
        return fmt switch
        {
            "xml" => ParseXml(filePath),
            "csv" => ParseCsv(filePath, csvSeparator),
            "xls" or "xlsx" or "excel" => ParseExcel(filePath),
            "pdf" => ParsePdf(filePath),
            _ => throw new ArgumentException($"Unsupported format: {fmt}")
        };
    }

    private string DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xml" => "xml",
            ".csv" => "csv",
            ".xls" => "xls",
            ".xlsx" => "xlsx",
            ".pdf" => "pdf",
            _ => "unknown"
        };
    }

    // ── XML Parser ──────────────────────────────────────────────────────────

    private (List<Dictionary<string, object?>> Records, List<string> Fields) ParseXml(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var records = new List<Dictionary<string, object?>>();
        var fieldSet = new HashSet<string>();

        if (doc.Root == null) return (records, fieldSet.ToList());

        // Find repeating elements (elements with same name siblings)
        var repeatingGroups = FindRepeatingElements(doc.Root);

        if (repeatingGroups.Count > 0)
        {
            foreach (var group in repeatingGroups)
            {
                foreach (var element in group)
                {
                    var record = new Dictionary<string, object?>();
                    FlattenElement(element, "", record);
                    foreach (var key in record.Keys) fieldSet.Add(key);
                    records.Add(record);
                }
            }
        }
        else
        {
            // Single-record document
            var record = new Dictionary<string, object?>();
            FlattenElement(doc.Root, "", record);
            foreach (var key in record.Keys) fieldSet.Add(key);
            records.Add(record);
        }

        return (records, fieldSet.OrderBy(f => f).ToList());
    }

    private List<List<XElement>> FindRepeatingElements(XElement root)
    {
        var groups = new List<List<XElement>>();
        var visited = new HashSet<string>();

        void Search(XElement el)
        {
            var childGroups = el.Elements()
                .GroupBy(c => c.Name.LocalName)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var grp in childGroups)
            {
                var key = $"{el.Name.LocalName}/{grp.Key}";
                if (!visited.Contains(key))
                {
                    visited.Add(key);
                    groups.Add(grp.ToList());
                }
            }

            foreach (var child in el.Elements())
            {
                if (!childGroups.Any(g => g.Key == child.Name.LocalName))
                    Search(child);
            }
        }

        Search(root);
        return groups;
    }

    private void FlattenElement(XElement el, string prefix, Dictionary<string, object?> record)
    {
        // Attributes
        foreach (var attr in el.Attributes())
        {
            var key = string.IsNullOrEmpty(prefix) ? $"@{attr.Name.LocalName}" : $"{prefix}.@{attr.Name.LocalName}";
            record[key] = attr.Value;
        }

        var children = el.Elements().ToList();
        if (children.Count == 0)
        {
            // Leaf node
            var key = string.IsNullOrEmpty(prefix) ? el.Name.LocalName : prefix;
            record[key] = el.Value?.Trim();
        }
        else
        {
            foreach (var child in children)
            {
                var childPrefix = string.IsNullOrEmpty(prefix)
                    ? child.Name.LocalName
                    : $"{prefix}.{child.Name.LocalName}";
                FlattenElement(child, childPrefix, record);
            }
        }
    }

    // ── CSV Parser ──────────────────────────────────────────────────────────

    private (List<Dictionary<string, object?>> Records, List<string> Fields) ParseCsv(
        string filePath, string? separator = null)
    {
        var records = new List<Dictionary<string, object?>>();
        var fieldSet = new HashSet<string>();

        var delimiter = ResolveSeparator(separator, filePath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        foreach (var h in headers) fieldSet.Add(h);

        while (csv.Read())
        {
            var record = new Dictionary<string, object?>();
            foreach (var header in headers)
            {
                record[header] = csv.GetField(header);
            }
            records.Add(record);
        }

        return (records, fieldSet.ToList());
    }

    private string ResolveSeparator(string? separator, string filePath)
    {
        if (!string.IsNullOrEmpty(separator))
        {
            return separator.ToLowerInvariant() switch
            {
                "comma" or "," => ",",
                "semicolon" or ";" => ";",
                "tab" or "\\t" => "\t",
                "pipe" or "|" => "|",
                _ => separator
            };
        }

        // Auto-detect
        try
        {
            using var sr = new StreamReader(filePath);
            var firstLine = sr.ReadLine() ?? "";
            var secondLine = sr.ReadLine() ?? "";
            var sample = firstLine + "\n" + secondLine;

            var candidates = new[] { ";", ",", "\t", "|" };
            var best = ",";
            var bestCount = 0;

            foreach (var sep in candidates)
            {
                var count = firstLine.Split(sep).Length;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = sep;
                }
            }
            return best;
        }
        catch
        {
            return ",";
        }
    }

    // ── Excel Parser ────────────────────────────────────────────────────────

    private (List<Dictionary<string, object?>> Records, List<string> Fields) ParseExcel(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var records = new List<Dictionary<string, object?>>();
        var fieldSet = new HashSet<string>();

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        if (dataSet.Tables.Count == 0) return (records, fieldSet.ToList());

        var table = dataSet.Tables[0];
        if (table.Rows.Count == 0) return (records, fieldSet.ToList());

        // Find header row (first row with >50% non-empty cells)
        int headerRowIdx = FindHeaderRow(table);
        var headers = new List<string>();

        for (int col = 0; col < table.Columns.Count; col++)
        {
            var val = table.Rows[headerRowIdx][col]?.ToString()?.Trim() ?? "";
            headers.Add(string.IsNullOrEmpty(val) ? $"Column{col + 1}" : val);
        }
        foreach (var h in headers) fieldSet.Add(h);

        for (int row = headerRowIdx + 1; row < table.Rows.Count; row++)
        {
            var record = new Dictionary<string, object?>();
            bool hasData = false;

            for (int col = 0; col < headers.Count && col < table.Columns.Count; col++)
            {
                var val = table.Rows[row][col];
                var strVal = val?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(strVal)) hasData = true;
                record[headers[col]] = strVal;
            }

            if (hasData) records.Add(record);
        }

        return (records, fieldSet.ToList());
    }

    private int FindHeaderRow(DataTable table)
    {
        for (int row = 0; row < Math.Min(10, table.Rows.Count); row++)
        {
            int nonEmpty = 0;
            for (int col = 0; col < table.Columns.Count; col++)
            {
                if (!string.IsNullOrWhiteSpace(table.Rows[row][col]?.ToString()))
                    nonEmpty++;
            }
            if (nonEmpty > table.Columns.Count * 0.5)
                return row;
        }
        return 0;
    }

    // ── PDF Parser ──────────────────────────────────────────────────────────

    private (List<Dictionary<string, object?>> Records, List<string> Fields) ParsePdf(string filePath)
    {
        var records = new List<Dictionary<string, object?>>();
        var fieldSet = new HashSet<string>();

        using var pdfReader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(pdfReader);

        var allLines = new List<string>();

        for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            var text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(page), strategy);
            allLines.AddRange(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }

        // Try to detect tabular data
        if (allLines.Count < 2)
            return (records, fieldSet.ToList());

        // Heuristic: split lines by multiple spaces
        var parsedRows = allLines
            .Select(line => System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s{2,}")
                .Select(c => c.Trim()).Where(c => c.Length > 0).ToArray())
            .Where(cols => cols.Length > 1)
            .ToList();

        if (parsedRows.Count < 2)
        {
            // Fallback: each line is a record with index + text
            for (int i = 0; i < allLines.Count; i++)
            {
                records.Add(new Dictionary<string, object?>
                {
                    ["LineNumber"] = (i + 1).ToString(),
                    ["Text"] = allLines[i].Trim()
                });
            }
            fieldSet.Add("LineNumber");
            fieldSet.Add("Text");
            return (records, fieldSet.ToList());
        }

        // First row with most columns = header
        var headerRow = parsedRows.OrderByDescending(r => r.Length).First();
        var headerIdx = parsedRows.IndexOf(headerRow);
        var headers = headerRow.Select((h, i) =>
            string.IsNullOrEmpty(h) ? $"Column{i + 1}" : h).ToList();
        foreach (var h in headers) fieldSet.Add(h);

        for (int i = headerIdx + 1; i < parsedRows.Count; i++)
        {
            var cols = parsedRows[i];
            var record = new Dictionary<string, object?>();
            for (int c = 0; c < headers.Count; c++)
            {
                record[headers[c]] = c < cols.Length ? cols[c] : "";
            }
            records.Add(record);
        }

        return (records, fieldSet.ToList());
    }
}
