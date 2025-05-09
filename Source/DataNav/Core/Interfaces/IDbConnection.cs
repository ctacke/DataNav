using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataNav.Core.Interfaces
{
    /// <summary>
    /// Represents a generic database connection
    /// </summary>
    public interface IDbConnection : IDisposable
    {
        /// <summary>
        /// Gets whether the connection is currently open
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the connection info used to establish this connection
        /// </summary>
        ConnectionInfo ConnectionInfo { get; }

        /// <summary>
        /// Opens the database connection
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Closes the database connection
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Gets a list of databases/schemas available on the server
        /// </summary>
        Task<IEnumerable<Database>> GetDatabasesAsync();

        /// <summary>
        /// Gets a list of tables in the specified database/schema
        /// </summary>
        Task<IEnumerable<Table>> GetTablesAsync(string databaseName);

        /// <summary>
        /// Gets the structure of the specified table
        /// </summary>
        Task<IEnumerable<Column>> GetColumnsAsync(string databaseName, string tableName);

        /// <summary>
        /// Executes a query and returns the result data
        /// </summary>
        Task<QueryResult> ExecuteQueryAsync(string query);
    }
}