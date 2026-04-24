using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels
{
    public class SqlDataColumn
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class SqlRecord
    {
        public string RecordLabel { get; set; } = string.Empty;
        public ObservableCollection<SqlDataColumn> Columns { get; set; } = new();
    }

    public partial class SqlDataViewModel : ObservableObject
    {
        private readonly SqlParser _sqlParser = new();
        private readonly TableDefinitionService _tableService = new();
        private List<LogEntry> _allEntries = new();
        private string _searchCallId = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> availableTables = new();

        [ObservableProperty]
        private string selectedTable = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SqlRecord> sqlRecords = new();

        [ObservableProperty]
        private string statusMessage = "Run analysis first";

        partial void OnSelectedTableChanged(string value)
        {
            RefreshTableData();
        }

        public SqlDataViewModel()
        {
            _tableService.LoadDefinitions();
            RefreshTableList();
        }

        public void SetData(List<LogEntry> entries, string callId)
        {
            _allEntries = entries;
            _searchCallId = callId;
            RefreshTableData();
        }

        private void RefreshTableList()
        {
            AvailableTables.Clear();
            foreach (var table in _tableService.GetAvailableTables())
                AvailableTables.Add(table);

            if (AvailableTables.Count > 0)
                SelectedTable = AvailableTables[0];
        }

        private void RefreshTableData()
        {
            SqlRecords.Clear();

            if (string.IsNullOrEmpty(SelectedTable) || string.IsNullOrEmpty(_searchCallId))
            {
                StatusMessage = "Run analysis first";
                return;
            }

            if (SelectedTable.Equals("Calls", StringComparison.OrdinalIgnoreCase))
                LoadCallsRecord();
            else if (SelectedTable.Equals("CallsQueues", StringComparison.OrdinalIgnoreCase))
                LoadCallsQueuesRecords();
            else if (SelectedTable.Equals("AgentCalls", StringComparison.OrdinalIgnoreCase))
                LoadAgentCallsRecords();
            else
                LoadGenericRecords();
        }

        private void LoadCallsRecord()
        {
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("insert into Calls", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (tableName, columns) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!tableName.Equals("Calls", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only the record where Id exactly matches the searchCallId
                if (!columns.TryGetValue("Id", out var id) ||
                    !id.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var record = new SqlRecord { RecordLabel = $"Calls — Id: {id}" };
                record.Columns = BuildColumns("Calls", columns);
                SqlRecords.Add(record);

                StatusMessage = $"Found 1 Calls record";
                return;
            }

            StatusMessage = "No Calls record found for this CallID";
        }

        private void LoadCallsQueuesRecords()
        {
            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("insert into CallsQueues", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (tableName, columns) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!tableName.Equals("CallsQueues", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only records where CallId matches the searchCallId
                if (!columns.TryGetValue("CallId", out var callId) ||
                    !callId.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var seqNum = columns.TryGetValue("SeqNumber", out var seq) ? seq : count.ToString();
                var record = new SqlRecord { RecordLabel = $"CallsQueues — Seq #{seqNum}" };
                record.Columns = BuildColumns("CallsQueues", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} CallsQueues record(s)"
                : "No CallsQueues records found for this CallID";
        }

        private void LoadAgentCallsRecords()
        {
            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (procName, columns) = _sqlParser.ParseExecStatement(entry.Message);
                if (!procName.Equals("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!columns.TryGetValue("CallRef", out var callRef) ||
                    !callRef.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var agentId = columns.TryGetValue("AgentID", out var aid) ? aid : count.ToString();
                var record = new SqlRecord { RecordLabel = $"AgentCalls — Agent: {agentId}" };
                record.Columns = BuildColumns("AgentCalls", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} AgentCalls record(s)"
                : "No AgentCalls records found for this CallID";
        }

        private void LoadGenericRecords()
        {
            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains($"insert into {SelectedTable}", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (tableName, columns) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!tableName.Equals(SelectedTable, StringComparison.OrdinalIgnoreCase))
                    continue;

                var record = new SqlRecord { RecordLabel = $"{SelectedTable} #{count}" };
                record.Columns = BuildColumns(SelectedTable, columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = $"Found {count} {SelectedTable} record(s)";
        }

        private ObservableCollection<SqlDataColumn> BuildColumns(
            string tableName, Dictionary<string, string> columns)
        {
            var result = new ObservableCollection<SqlDataColumn>();
            foreach (var col in columns)
            {
                result.Add(new SqlDataColumn
                {
                    Name = col.Key,
                    DisplayValue = _tableService.FormatFieldValue(tableName, col.Key, col.Value),
                    Description = _tableService.GetFieldDescription(tableName, col.Key),
                    Type = _tableService.GetFieldType(tableName, col.Key)
                });
            }
            return result;
        }
    }
}
