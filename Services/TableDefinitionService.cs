using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    public class TableDefinitionService
    {
        private readonly Dictionary<string, TableDefinition> _tableDefinitions = new();
        private readonly MarkdownTableParser _parser = new();

        // Maps table name → .md filename stem (without _table_reference.md)
        private static readonly Dictionary<string, string> TableFileMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Calls",       "Calls_table_reference" },
            { "CallsQueues", "CallsQueues_table_reference" },
            { "AgentCalls",  "AgentCalls_table_reference" }
        };

        public void LoadDefinitions()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            foreach (var (tableName, fileStem) in TableFileMap)
            {
                var resourceName = $"LogAnalyzer.Data.{fileStem}.md";
                var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    var table = _parser.ParseContent(content, tableName, tableName);
                    _tableDefinitions[tableName] = table;
                }
            }
        }

        public TableDefinition? GetTableDefinition(string tableName)
        {
            _tableDefinitions.TryGetValue(tableName, out var def);
            return def;
        }

        public List<string> GetAvailableTables() =>
            _tableDefinitions.Keys.OrderBy(k => k).ToList();

        public string FormatFieldValue(string tableName, string fieldName, string value)
        {
            if (_tableDefinitions.TryGetValue(tableName, out var table) &&
                table.Fields.TryGetValue(fieldName, out var field) &&
                field.ValueMappings != null &&
                field.ValueMappings.TryGetValue(value, out var mapped))
            {
                return $"{mapped} ({value})";
            }
            return value;
        }

        public string GetFieldDescription(string tableName, string fieldName)
        {
            if (_tableDefinitions.TryGetValue(tableName, out var table) &&
                table.Fields.TryGetValue(fieldName, out var field))
                return field.Description;
            return "";
        }

        public string GetFieldType(string tableName, string fieldName)
        {
            if (_tableDefinitions.TryGetValue(tableName, out var table) &&
                table.Fields.TryGetValue(fieldName, out var field))
                return field.Type;
            return "";
        }
    }
}
