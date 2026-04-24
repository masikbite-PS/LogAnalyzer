using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogAnalyzer.Services
{
    public class SqlParser
    {
        public (string tableName, Dictionary<string, string> columns) ParseInsertStatement(string sql)
        {
            var match = Regex.Match(sql,
                @"insert\s+into\s+([^\(]+)\s*\(([^\)]+)\)\s*values\s*\(([^\)]+)\)",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return (string.Empty, new Dictionary<string, string>());

            var tableName = match.Groups[1].Value.Trim();
            var columnNames = match.Groups[2].Value.Split(',')
                .Select(c => c.Trim()).ToList();
            var columnValues = ParseValues(match.Groups[3].Value);

            var result = new Dictionary<string, string>();
            for (int i = 0; i < columnNames.Count && i < columnValues.Count; i++)
            {
                var value = columnValues[i];
                // Skip NULL values
                if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                    continue;

                result[columnNames[i]] = UnquoteValue(value);
            }

            return (tableName, result);
        }

        private List<string> ParseValues(string valueString)
        {
            var values = new List<string>();
            var current = string.Empty;
            var inQuote = false;
            var quoteChar = '\0';
            var i = 0;

            while (i < valueString.Length)
            {
                var c = valueString[i];

                if ((c == '\'' || c == '"') && (i == 0 || valueString[i - 1] != '\\'))
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = c;
                        current += c;
                    }
                    else if (c == quoteChar)
                    {
                        inQuote = false;
                        current += c;
                    }
                    else
                    {
                        current += c;
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    values.Add(current.Trim());
                    current = string.Empty;
                }
                else
                {
                    current += c;
                }

                i++;
            }

            if (!string.IsNullOrEmpty(current))
                values.Add(current.Trim());

            return values;
        }

        private string UnquoteValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if ((value.StartsWith("'") && value.EndsWith("'")) ||
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                return value.Substring(1, value.Length - 2)
                    .Replace("\\'", "'")
                    .Replace("\\\"", "\"");
            }

            return value;
        }
    }
}
