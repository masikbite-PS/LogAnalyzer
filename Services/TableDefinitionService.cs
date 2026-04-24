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
            { "Calls",      "Calls_table_reference" },
            { "CallsQueues", "CallsQueues_table_reference" }
        };

        public void LoadDefinitions()
        {
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDir = Path.GetDirectoryName(assemblyPath) ?? ".";
            var dataDir = Path.Combine(appDir, "Data");

            foreach (var (tableName, fileStem) in TableFileMap)
            {
                var filePath = Path.Combine(dataDir, fileStem + ".md");
                var table = _parser.ParseFile(filePath, tableName, tableName);
                _tableDefinitions[tableName] = table;
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
