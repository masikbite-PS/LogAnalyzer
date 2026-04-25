using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        public string TableName { get; set; } = string.Empty;
        public ObservableCollection<SqlDataColumn> Columns { get; set; } = new();
    }

    public partial class SqlDataViewModel : ObservableObject
    {
        private readonly SqlParser _sqlParser = new();
        private readonly TableDefinitionService _tableService = new();
        private List<LogEntry> _allEntries = new();
        private string _searchCallId = string.Empty;
        private string _partnerPhysicalId = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> availableTables = new();

        [ObservableProperty]
        private string selectedTable = string.Empty;

        [ObservableProperty]
        private ObservableCollection<SqlRecord> sqlRecords = new();

        [ObservableProperty]
        private string statusMessage = "Run analysis first";

        [RelayCommand]
        public void ExportToTextile()
        {
            if (SqlRecords.Count == 0)
            {
                MessageBox.Show("No data to export", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var textile = GenerateTextileFormat();
            var dialog = new TextileExportDialog(textile);
            dialog.ShowDialog();
        }

        private string GenerateTextileFormat()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"h3. {SelectedTable} Data");
            sb.AppendLine();

            foreach (var record in SqlRecords)
            {
                sb.AppendLine($"h4. {record.RecordLabel}");
                sb.AppendLine();

                // Create a Textile table
                sb.AppendLine("|_.Field|_.Value|_.Type|_.Description|");
                foreach (var column in record.Columns)
                {
                    var field = EscapeTextile(column.Name);
                    var value = EscapeTextile(column.DisplayValue);
                    var type = EscapeTextile(column.Type);
                    var desc = EscapeTextile(column.Description);

                    sb.AppendLine($"|{field}|{value}|{type}|{desc}|");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeTextile(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Replace line breaks with space for table cells
            return text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        partial void OnSelectedTableChanged(string value)
        {
            RefreshTableData();
        }

        public SqlDataViewModel()
        {
            _tableService.LoadDefinitions();
            RefreshTableList();
        }

        public void SetData(List<LogEntry> entries, string callId, string partnerPhysicalId = "")
        {
            _allEntries = entries;
            _searchCallId = callId;
            _partnerPhysicalId = partnerPhysicalId;
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
            else if (SelectedTable.Equals("PhysicalCalls", StringComparison.OrdinalIgnoreCase))
                LoadPhysicalCallsRecords();
            else if (SelectedTable.Equals("AgentLogins", StringComparison.OrdinalIgnoreCase))
                LoadAgentLoginsRecords();
            else if (SelectedTable.Equals("AgentStates", StringComparison.OrdinalIgnoreCase))
                LoadAgentStatesRecords();
            else if (SelectedTable.Equals("AgentServices", StringComparison.OrdinalIgnoreCase))
                LoadAgentServicesRecords();
            else if (SelectedTable.Equals("UserBindingDevice", StringComparison.OrdinalIgnoreCase))
                LoadUserBindingDeviceRecords();
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

                var record = new SqlRecord { RecordLabel = $"Calls — Id: {id}", TableName = "Calls" };
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
                var record = new SqlRecord { RecordLabel = $"CallsQueues — Seq #{seqNum}", TableName = "CallsQueues" };
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
                var record = new SqlRecord { RecordLabel = $"AgentCalls — Agent: {agentId}", TableName = "AgentCalls" };
                record.Columns = BuildColumns("AgentCalls", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} AgentCalls record(s)"
                : "No AgentCalls records found for this CallID";
        }

        private void LoadPhysicalCallsRecords()
        {
            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("insert into PhysicalCalls", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (tableName, columns) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!tableName.Equals("PhysicalCalls", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filter by InitialCallId (= SearchCallID) or Id (= PartnerID)
                bool matchesSearchCallId = false;
                if (columns.TryGetValue("InitialCallId", out var initialCallId) &&
                    initialCallId.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase))
                {
                    matchesSearchCallId = true;
                }

                bool matchesPartnerId = false;
                if (!string.IsNullOrEmpty(_partnerPhysicalId) &&
                    columns.TryGetValue("Id", out var id) &&
                    id.Equals(_partnerPhysicalId, StringComparison.OrdinalIgnoreCase))
                {
                    matchesPartnerId = true;
                }

                if (!matchesSearchCallId && !matchesPartnerId)
                    continue;

                var label = columns.TryGetValue("Id", out var physicalId) ? physicalId : count.ToString();
                var record = new SqlRecord { RecordLabel = $"PhysicalCalls — Id: {label}", TableName = "PhysicalCalls" };
                record.Columns = BuildColumns("PhysicalCalls", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} PhysicalCalls record(s)"
                : "No PhysicalCalls records found for this CallID or PartnerID";
        }

        private void LoadAgentLoginsRecords()
        {
            var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (procName, columns) = _sqlParser.ParseExecStatement(entry.Message);
                if (!procName.Equals("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (columns.TryGetValue("CallRef", out var callRef) &&
                    callRef.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase) &&
                    columns.TryGetValue("AgentID", out var agentId))
                {
                    agentIds.Add(agentId);
                }
            }

            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentLogins", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentLogins", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentLogin", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (!columns.TryGetValue("UserLogin", out var userLogin) ||
                    !agentIds.Contains(userLogin))
                    continue;

                var sessionId = columns.TryGetValue("SessionId", out var sid) ? sid : count.ToString();
                var record = new SqlRecord { RecordLabel = $"AgentLogins — Session: {sessionId}", TableName = "AgentLogins" };
                record.Columns = BuildColumns("AgentLogins", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} AgentLogins record(s)"
                : "No AgentLogins records found for this CallID's agents";
        }

        private void LoadAgentStatesRecords()
        {
            var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (procName, columns) = _sqlParser.ParseExecStatement(entry.Message);
                if (!procName.Equals("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (columns.TryGetValue("CallRef", out var callRef) &&
                    callRef.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase) &&
                    columns.TryGetValue("AgentID", out var agentId))
                {
                    agentIds.Add(agentId);
                }
            }

            var sessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentLogins", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentLogins", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentLogin", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (columns.TryGetValue("UserLogin", out var userLogin) &&
                    agentIds.Contains(userLogin) &&
                    columns.TryGetValue("SessionId", out var sessionId))
                {
                    sessionIds.Add(sessionId);
                }
            }

            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentStates", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentStates", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentState", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (!columns.TryGetValue("SessionId", out var sessionId) ||
                    !sessionIds.Contains(sessionId))
                    continue;

                var stateType = columns.TryGetValue("StateType", out var st) ? st : count.ToString();
                var record = new SqlRecord { RecordLabel = $"AgentStates — State: {stateType}", TableName = "AgentStates" };
                record.Columns = BuildColumns("AgentStates", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} AgentStates record(s)"
                : "No AgentStates records found for this CallID's agent sessions";
        }

        private void LoadAgentServicesRecords()
        {
            var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (procName, columns) = _sqlParser.ParseExecStatement(entry.Message);
                if (!procName.Equals("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (columns.TryGetValue("CallRef", out var callRef) &&
                    callRef.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase) &&
                    columns.TryGetValue("AgentID", out var agentId))
                {
                    agentIds.Add(agentId);
                }
            }

            var sessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentLogins", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentLogins", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentLogin", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (columns.TryGetValue("UserLogin", out var userLogin) &&
                    agentIds.Contains(userLogin) &&
                    columns.TryGetValue("SessionId", out var sessionId))
                {
                    sessionIds.Add(sessionId);
                }
            }

            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentServices", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentServices", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentService", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (!columns.TryGetValue("SessionId", out var sessionId) ||
                    !sessionIds.Contains(sessionId))
                    continue;

                var serviceNum = columns.TryGetValue("ServiceNumber", out var sn) ? sn : count.ToString();
                var record = new SqlRecord { RecordLabel = $"AgentServices — Service: {serviceNum}", TableName = "AgentServices" };
                record.Columns = BuildColumns("AgentServices", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} AgentServices record(s)"
                : "No AgentServices records found for this CallID's agent sessions";
        }

        private void LoadUserBindingDeviceRecords()
        {
            var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (procName, columns) = _sqlParser.ParseExecStatement(entry.Message);
                if (!procName.Equals("InsertAgentCall", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (columns.TryGetValue("CallRef", out var callRef) &&
                    callRef.Equals(_searchCallId, StringComparison.OrdinalIgnoreCase) &&
                    columns.TryGetValue("AgentID", out var agentId))
                {
                    agentIds.Add(agentId);
                }
            }

            var sessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("AgentLogins", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("AgentLogins", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertAgentLogin", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (columns.TryGetValue("UserLogin", out var userLogin) &&
                    agentIds.Contains(userLogin) &&
                    columns.TryGetValue("SessionId", out var sessionId))
                {
                    sessionIds.Add(sessionId);
                }
            }

            int count = 0;
            foreach (var entry in _allEntries)
            {
                if (!entry.Message.Contains("UserBindingDevice", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, string> columns;
                if (entry.Message.Contains("insert into", StringComparison.OrdinalIgnoreCase))
                {
                    var (tableName, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                    if (!tableName.Equals("UserBindingDevice", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }
                else
                {
                    var (procName, cols) = _sqlParser.ParseExecStatement(entry.Message);
                    if (!procName.Equals("InsertUserBindingDevice", StringComparison.OrdinalIgnoreCase))
                        continue;
                    columns = cols;
                }

                if (!columns.TryGetValue("SessionId", out var sessionId) ||
                    !sessionIds.Contains(sessionId))
                    continue;

                var actionType = columns.TryGetValue("ActionType", out var at) ? at : count.ToString();
                var record = new SqlRecord { RecordLabel = $"UserBindingDevice — Action: {actionType}", TableName = "UserBindingDevice" };
                record.Columns = BuildColumns("UserBindingDevice", columns);
                SqlRecords.Add(record);
                count++;
            }

            StatusMessage = count > 0
                ? $"Found {count} UserBindingDevice record(s)"
                : "No UserBindingDevice records found for this CallID's agent sessions";
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

                var record = new SqlRecord { RecordLabel = $"{SelectedTable} #{count}", TableName = SelectedTable };
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
