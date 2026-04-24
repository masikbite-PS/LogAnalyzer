using System.Collections.Generic;

namespace LogAnalyzer.Models
{
    public class SqlDataRow
    {
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, (string value, string description)> Columns { get; set; } = new();
    }

    public class TableFieldDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public Dictionary<string, string>? ValueMappings { get; set; }
    }

    public class TableDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Dictionary<string, TableFieldDefinition> Fields { get; set; } = new();
    }
}
