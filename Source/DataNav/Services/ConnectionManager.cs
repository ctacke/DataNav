using DataNav.Core;
using DataNav.Core.Interfaces;
using DataNav.Providers.Cassandra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataNav.Services
{
    /// <summary>
    /// Event arguments for connection events
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the connection associated with the event
        /// </summary>
        public IDbConnection Connection { get; }

        /// <summary>
        /// Gets the connection name
        /// </summary>
        public string ConnectionName { get; }

        /// <summary>
        /// Initializes a new instance of the ConnectionEventArgs class
        /// </summary>
        public ConnectionEventArgs(IDbConnection connection)
        {
            Connection = connection;
            ConnectionName = connection?.ConnectionInfo?.Name;
        }
    }

    /// <summary>
    /// Manages database connections
    /// </summary>
    public class ConnectionManager
    {
        private readonly Dictionary<string, IDbConnection> _connections = new Dictionary<string, IDbConnection>();
        private readonly Dictionary<string, Func<ConnectionInfo, IDbConnection>> _providerFactories = new Dictionary<string, Func<ConnectionInfo, IDbConnection>>();

        /// <summary>
        /// Event raised when a connection is added
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionAdded;

        /// <summary>
        /// Event raised when a connection is removed
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionRemoved;

        /// <summary>
        /// Event raised when a connection state changes
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionStateChanged;

        /// <summary>
        /// Initializes a new instance of the ConnectionManager class
        /// </summary>
        public ConnectionManager()
        {
            // Register database providers
            RegisterProvider("cassandra", info => new CassandraProvider(info));
            // Register other providers here as they are implemented
            // RegisterProvider("postgresql", info => new PostgreSqlProvider(info));
            // RegisterProvider("sqlserver", info => new SqlServerProvider(info));
        }

        /// <summary>
        /// Registers a database provider factory
        /// </summary>
        /// <param name="providerType">The provider type identifier</param>
        /// <param name="factory">The factory function to create provider instances</param>
        public void RegisterProvider(string providerType, Func<ConnectionInfo, IDbConnection> factory)
        {
            if (string.IsNullOrWhiteSpace(providerType))
                throw new ArgumentException("Provider type cannot be empty", nameof(providerType));

            _providerFactories[providerType.ToLowerInvariant()] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Gets a list of supported database provider types
        /// </summary>
        public IEnumerable<string> GetSupportedProviders()
        {
            return _providerFactories.Keys.ToList();
        }

        /// <summary>
        /// Gets a list of active connections
        /// </summary>
        public IEnumerable<IDbConnection> GetConnections()
        {
            return _connections.Values.ToList();
        }

        /// <summary>
        /// Creates and adds a new database connection
        /// </summary>
        /// <param name="connectionInfo">The connection information</param>
        /// <returns>The new connection instance</returns>
        public IDbConnection AddConnection(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
                throw new ArgumentNullException(nameof(connectionInfo));

            if (string.IsNullOrWhiteSpace(connectionInfo.Name))
                throw new ArgumentException("Connection name cannot be empty", nameof(connectionInfo));

            if (_connections.ContainsKey(connectionInfo.Name))
                throw new InvalidOperationException($"A connection named '{connectionInfo.Name}' already exists");

            if (!_providerFactories.TryGetValue(connectionInfo.ProviderType.ToLowerInvariant(), out var factory))
                throw new ArgumentException($"Unsupported provider type: {connectionInfo.ProviderType}");

            var connection = factory(connectionInfo);
            _connections[connectionInfo.Name] = connection;

            // Raise event
            ConnectionAdded?.Invoke(this, new ConnectionEventArgs(connection));

            return connection;
        }

        /// <summary>
        /// Gets a connection by name
        /// </summary>
        /// <param name="name">The connection name</param>
        /// <returns>The connection, or null if not found</returns>
        public IDbConnection GetConnection(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Connection name cannot be empty", nameof(name));

            _connections.TryGetValue(name, out var connection);
            return connection;
        }

        /// <summary>
        /// Removes a connection
        /// </summary>
        /// <param name="name">The connection name</param>
        /// <returns>True if the connection was removed, false if not found</returns>
        public bool RemoveConnection(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Connection name cannot be empty", nameof(name));

            if (!_connections.TryGetValue(name, out var connection))
                return false;

            // Disconnect if connected
            if (connection.IsConnected)
            {
                connection.DisconnectAsync().Wait();
            }

            // Remove from dictionary
            _connections.Remove(name);

            // Dispose resources
            connection.Dispose();

            // Raise event
            ConnectionRemoved?.Invoke(this, new ConnectionEventArgs(connection));

            return true;
        }

        /// <summary>
        /// Opens a connection
        /// </summary>
        /// <param name="name">The connection name</param>
        /// <returns>True if connected successfully, false otherwise</returns>
        public async Task<bool> ConnectAsync(string name)
        {
            var connection = GetConnection(name);
            if (connection == null)
                return false;

            try
            {
                var success = await connection.ConnectAsync();

                // Raise event
                ConnectionStateChanged?.Invoke(this, new ConnectionEventArgs(connection));

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to {name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Closes a connection
        /// </summary>
        /// <param name="name">The connection name</param>
        /// <returns>The task</returns>
        public async Task DisconnectAsync(string name)
        {
            var connection = GetConnection(name);
            if (connection == null)
                return;

            try
            {
                await connection.DisconnectAsync();

                // Raise event
                ConnectionStateChanged?.Invoke(this, new ConnectionEventArgs(connection));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from {name}: {ex.Message}");
            }
        }
    }
}