using Cassandra;
using DataNav.Core;
using DataNav.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace DataNav.Providers.Cassandra
{
    /// <summary>
    /// Provides connectivity to Cassandra/ScyllaDB databases
    /// </summary>
    public class CassandraProvider : IDbConnection
    {
        private bool _isConnected;
        private ISession _session;
        private Cluster _cluster;

        /// <summary>
        /// Gets whether the connection is currently open
        /// </summary>
        public bool IsConnected => _isConnected && _session != null && !_session.IsDisposed;

        /// <summary>
        /// Gets the connection info used to establish this connection
        /// </summary>
        public ConnectionInfo ConnectionInfo { get; }

        /// <summary>
        /// Initializes a new instance of the CassandraProvider class
        /// </summary>
        /// <param name="connectionInfo">The connection information</param>
        public CassandraProvider(ConnectionInfo connectionInfo)
        {
            ConnectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
        }

        /// <summary>
        /// Opens the database connection
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                Console.WriteLine($"Connecting to Cassandra: {ConnectionInfo.Host}:{ConnectionInfo.Port}");

                // Create a builder for the cluster connection
                var builder = Cluster.Builder()
                    .AddContactPoint(ConnectionInfo.Host)
                    .WithPort(ConnectionInfo.Port);

                // Add credentials if provided
                if (!string.IsNullOrEmpty(ConnectionInfo.Username))
                {
                    builder = builder.WithCredentials(ConnectionInfo.Username, ConnectionInfo.Password);
                }

                // Add SSL if requested
                if (ConnectionInfo.UseSsl)
                {
                    builder = builder.WithSSL(new SSLOptions().SetRemoteCertValidationCallback(
                        (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true));
                }

                // Get connection timeout if specified
                if (ConnectionInfo.Options.TryGetValue("Timeout", out var timeoutStr) &&
                    int.TryParse(timeoutStr, out var timeout) && timeout > 0)
                {
                    builder = builder.WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(timeout));
                }

                // Create the cluster
                _cluster = builder.Build();

                // Connect to the cluster
                _session = await Task.Run(() => _cluster.Connect());

                _isConnected = true;
                Console.WriteLine("Connected to Cassandra successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Cassandra: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Closes the database connection
        /// </summary>
        public Task DisconnectAsync()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            if (_cluster != null)
            {
                _cluster.Dispose();
                _cluster = null;
            }

            _isConnected = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a list of keyspaces available on the server
        /// </summary>
        public async Task<IEnumerable<Database>> GetDatabasesAsync()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to Cassandra");

            try
            {
                // Query for keyspaces
                string query = "SELECT keyspace_name FROM system_schema.keyspaces";
                var resultSet = await _session.ExecuteAsync(new SimpleStatement(query));

                var databases = new List<Database>();
                foreach (var row in resultSet)
                {
                    string keyspaceName = row.GetValue<string>("keyspace_name");

                    // Skip system keyspaces unless specifically requested in options
                    bool includeSystem = false;
                    ConnectionInfo.Options.TryGetValue("IncludeSystemKeyspaces", out var includeSystemStr);
                    bool.TryParse(includeSystemStr, out includeSystem);

                    if (includeSystem || (!keyspaceName.StartsWith("system") && !keyspaceName.StartsWith("dse")))
                    {
                        databases.Add(new Database { Name = keyspaceName });
                    }
                }

                Console.WriteLine($"Found {databases.Count} keyspaces");
                return databases;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting keyspaces: {ex.Message}");
                return Enumerable.Empty<Database>();
            }
        }

        /// <summary>
        /// Gets a list of tables in the specified keyspace
        /// </summary>
        public async Task<IEnumerable<Table>> GetTablesAsync(string keyspaceName)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to Cassandra");

            try
            {
                // Query for tables in the keyspace
                string query = $"SELECT table_name FROM system_schema.tables WHERE keyspace_name = '{keyspaceName}'";
                var resultSet = await _session.ExecuteAsync(new SimpleStatement(query));

                var tables = new List<Table>();
                foreach (var row in resultSet)
                {
                    string tableName = row.GetValue<string>("table_name");
                    tables.Add(new Table
                    {
                        Name = tableName,
                        Database = keyspaceName
                    });
                }

                Console.WriteLine($"Found {tables.Count} tables in keyspace {keyspaceName}");
                return tables;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tables for keyspace {keyspaceName}: {ex.Message}");
                return Enumerable.Empty<Table>();
            }
        }

        /// <summary>
        /// Gets the structure of the specified table
        /// </summary>
        public async Task<IEnumerable<Column>> GetColumnsAsync(string keyspaceName, string tableName)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to Cassandra");

            try
            {
                // Query for columns
                string columnsQuery = $"SELECT column_name, type FROM system_schema.columns WHERE keyspace_name = '{keyspaceName}' AND table_name = '{tableName}'";
                var columnsResult = await _session.ExecuteAsync(new SimpleStatement(columnsQuery));

                // Query for primary key
                string pkQuery = $"SELECT column_name FROM system_schema.columns " +
                                 $"WHERE keyspace_name = '{keyspaceName}' AND table_name = '{tableName}' " +
                                 $"AND kind = 'partition_key'";
                var pkResult = await _session.ExecuteAsync(new SimpleStatement(pkQuery));

                // Get partition key columns
                var primaryKeyColumns = new HashSet<string>();
                foreach (var row in pkResult)
                {
                    primaryKeyColumns.Add(row.GetValue<string>("column_name"));
                }

                // Get clustering key columns
                string clusteringQuery = $"SELECT column_name FROM system_schema.columns " +
                                        $"WHERE keyspace_name = '{keyspaceName}' AND table_name = '{tableName}' " +
                                        $"AND kind = 'clustering'";
                var clusteringResult = await _session.ExecuteAsync(new SimpleStatement(clusteringQuery));

                foreach (var row in clusteringResult)
                {
                    primaryKeyColumns.Add(row.GetValue<string>("column_name"));
                }

                // Build column list
                var columns = new List<Column>();
                foreach (var row in columnsResult)
                {
                    string columnName = row.GetValue<string>("column_name");
                    string dataType = row.GetValue<string>("type");

                    columns.Add(new Column
                    {
                        Name = columnName,
                        DataType = dataType,
                        IsPrimaryKey = primaryKeyColumns.Contains(columnName),
                        IsNullable = !primaryKeyColumns.Contains(columnName) // Primary key columns are not nullable
                    });
                }

                Console.WriteLine($"Found {columns.Count} columns for table {keyspaceName}.{tableName}");
                return columns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting columns for table {keyspaceName}.{tableName}: {ex.Message}");
                return Enumerable.Empty<Column>();
            }
        }

        /// <summary>
        /// Executes a query and returns the result data
        /// </summary>
        public async Task<QueryResult> ExecuteQueryAsync(string query)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to Cassandra");

            try
            {
                var startTime = DateTime.Now;
                var resultSet = await _session.ExecuteAsync(new SimpleStatement(query));
                var endTime = DateTime.Now;

                var executionTime = (long)(endTime - startTime).TotalMilliseconds;

                var result = new QueryResult
                {
                    ExecutionTime = executionTime
                };

                // If this is a SELECT statement, process rows
                if (resultSet.Any())
                {
                    // Get column definitions from the first row
                    var firstRow = resultSet.First();
                    var columnDefinitions = resultSet.Columns;
                    foreach (var column in columnDefinitions)
                    {
                        result.Columns.Add(new Column
                        {
                            Name = column.Name,
                            DataType = column.Type.Name
                        });
                    }

                    // Process all rows
                    foreach (var row in resultSet)
                    {
                        var rowData = new Dictionary<string, object>();
                        foreach (var column in columnDefinitions)
                        {
                            object value = null;
                            try
                            {
                                if (!row.IsNull(column.Name))
                                {
                                    // Try to get the value based on the type
                                    switch (column.Type.Name.ToLower())
                                    {
                                        case "text":
                                        case "varchar":
                                        case "ascii":
                                            value = row.GetValue<string>(column.Name);
                                            break;
                                        case "int":
                                            value = row.GetValue<int>(column.Name);
                                            break;
                                        case "bigint":
                                            value = row.GetValue<long>(column.Name);
                                            break;
                                        case "boolean":
                                            value = row.GetValue<bool>(column.Name);
                                            break;
                                        case "double":
                                            value = row.GetValue<double>(column.Name);
                                            break;
                                        case "float":
                                            value = row.GetValue<float>(column.Name);
                                            break;
                                        case "decimal":
                                            value = row.GetValue<decimal>(column.Name);
                                            break;
                                        case "timestamp":
                                            value = row.GetValue<DateTimeOffset>(column.Name).DateTime;
                                            break;
                                        case "uuid":
                                            value = row.GetValue<Guid>(column.Name);
                                            break;
                                        default:
                                            // Try to get as string for unsupported types
                                            try
                                            {
                                                value = row.GetValue<string>(column.Name);
                                            }
                                            catch
                                            {
                                                value = $"[Unsupported type: {column.Type.Name}]";
                                            }
                                            break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error getting value for column {column.Name}: {ex.Message}");
                                // If we can't get the value, use a placeholder
                                value = $"[Error: {ex.Message}]";
                            }

                            rowData[column.Name] = value;
                        }

                        result.Rows.Add(rowData);
                    }
                }
                else
                {
                    // For non-SELECT statements (INSERT, UPDATE, DELETE), we don't get rows back
                    // but we can still return the execution time
                    result.RowsAffected = -1; // Unknown number affected
                }

                Console.WriteLine($"Query executed: {result.Rows.Count} rows returned, {executionTime}ms");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
                throw; // Rethrow to let the caller handle it
            }
        }

        /// <summary>
        /// Disposes resources used by the provider
        /// </summary>
        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}