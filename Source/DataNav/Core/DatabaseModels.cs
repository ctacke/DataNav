using System.Collections.Generic;

namespace DataNav.Core
{
    /// <summary>
    /// Contains connection information for a database server
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// Gets or sets the display name for the connection
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of database provider
        /// </summary>
        public string ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the server hostname or IP address
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the username for authentication
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password for authentication
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets whether to use SSL/TLS for the connection
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Gets or sets additional provider-specific connection options
        /// </summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents a database or schema
    /// </summary>
    public class Database
    {
        /// <summary>
        /// Gets or sets the database name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets additional database properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a database table or collection
    /// </summary>
    public class Table
    {
        /// <summary>
        /// Gets or sets the table name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the database this table belongs to
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Gets or sets additional table properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a column in a database table
    /// </summary>
    public class Column
    {
        /// <summary>
        /// Gets or sets the column name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the data type
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Gets or sets whether this column is part of the primary key
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Gets or sets whether this column allows null values
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Gets or sets additional column properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents the result of a database query
    /// </summary>
    public class QueryResult
    {
        /// <summary>
        /// Gets or sets the column definitions
        /// </summary>
        public List<Column> Columns { get; set; } = new List<Column>();

        /// <summary>
        /// Gets or sets the result rows
        /// </summary>
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();

        /// <summary>
        /// Gets or sets the execution time in milliseconds
        /// </summary>
        public long ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the number of rows affected (for update operations)
        /// </summary>
        public long RowsAffected { get; set; }
    }
}