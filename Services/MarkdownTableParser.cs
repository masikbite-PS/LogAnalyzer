using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    public class MarkdownTableParser
    {
        private static readonly Regex ValueMappingRegex = new(
            @"(\d+)\s*[—–-]\s*([^<\n\r]+?)(?:<br/>|<br\s*/>|\s*$)",
            RegexOptions.Compiled
        );

        public TableDefinition ParseFile(string filePath, string tableName, string displayName)
        {
            var table = new TableDefinition { Name = tableName, DisplayName = displayName };

            if (!File.Exists(filePath))
                return table;

            var lines = File.ReadAllLines(filePath);
            return ParseLines(lines, tableName, displayName);
        }

        public TableDefinition ParseContent(string content, string tableName, string displayName)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return ParseLines(lines, tableName, displayName);
        }

        private TableDefinition ParseLines(string[] lines, string tableName, string displayName)
        {
            var table = new TableDefinition { Name = tableName, DisplayName = displayName };
            int nameIdx = -1, typeIdx = -1, descIdx = -1;
            bool headerFound = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("|")) continue;

                var cells = SplitTableRow(line);
                if (cells.Count == 0) continue;

                if (!headerFound)
                {
                    var headers = cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
                    nameIdx = headers.IndexOf("name");
                    typeIdx = headers.IndexOf("type");
                    descIdx = headers.IndexOf("description");
                    if (nameIdx >= 0 && descIdx >= 0)
                        headerFound = true;
                    continue;
                }

                if (cells.All(c => Regex.IsMatch(c.Trim(), @"^[-: ]+$")))
                    continue;

                if (nameIdx >= cells.Count || descIdx >= cells.Count)
                    continue;

                var fieldName = cells[nameIdx].Trim().Trim('*');
                if (string.IsNullOrEmpty(fieldName)) continue;

                var fieldType = typeIdx >= 0 && typeIdx < cells.Count ? cells[typeIdx].Trim() : "";
                var rawDesc = descIdx < cells.Count ? cells[descIdx] : "";

                table.Fields[fieldName] = new TableFieldDefinition
                {
                    Name = fieldName,
                    Type = fieldType,
                    Description = ExtractDescription(rawDesc),
                    ValueMappings = ExtractValueMappings(rawDesc)
                };
            }

            return table;
        }

        private List<string> SplitTableRow(string line)
        {
            var parts = line.Split('|');
            return parts.Skip(1).Take(parts.Length - 2).ToList();
        }

        private string ExtractDescription(string text)
        {
            var idx = text.IndexOf("Possible values", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                text = text.Substring(0, idx);

            text = Regex.Replace(text, @"<br\s*/?>", " ");
            text = Regex.Replace(text, @"<[^>]+>", "");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim().TrimEnd('.');
        }

        private Dictionary<string, string>? ExtractValueMappings(string text)
        {
            var matches = ValueMappingRegex.Matches(text);
            if (matches.Count == 0) return null;

            var mappings = new Dictionary<string, string>();
            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value.Trim();
                var value = Regex.Replace(match.Groups[2].Value.Trim(), @"<[^>]+>", "").Trim().TrimEnd(',', '.', ';');
                if (!string.IsNullOrEmpty(value))
                    mappings[key] = value;
            }
            return mappings.Count > 0 ? mappings : null;
        }
    }
}
