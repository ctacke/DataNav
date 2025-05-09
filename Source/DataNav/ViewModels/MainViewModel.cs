using Avalonia.Controls;
using DataNav.Core.Interfaces;
using DataNav.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DataNav.ViewModels
{
    /// <summary>
    /// ViewModel for the main window
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ConnectionManager _connectionManager;
        private IDbConnection _selectedConnection;
        private string _selectedDatabase;
        private string _selectedTable;

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the list of server connections
        /// </summary>
        public ObservableCollection<ServerViewModel> Servers { get; } = new ObservableCollection<ServerViewModel>();

        /// <summary>
        /// Gets or sets the selected connection
        /// </summary>
        public IDbConnection SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_selectedConnection != value)
                {
                    _selectedConnection = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsConnectionSelected));
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected database
        /// </summary>
        public string SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (_selectedDatabase != value)
                {
                    _selectedDatabase = value;
                    OnPropertyChanged();
                    LoadTablesAsync().ContinueWith(_ => { });
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected table
        /// </summary>
        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != value)
                {
                    _selectedTable = value;
                    OnPropertyChanged();
                    // TODO: Load table data
                }
            }
        }

        /// <summary>
        /// Gets the tables in the selected database
        /// </summary>
        public ObservableCollection<string> Tables { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Gets whether a connection is selected
        /// </summary>
        public bool IsConnectionSelected => SelectedConnection != null;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class
        /// </summary>
        /// <param name="connectionManager">The connection manager</param>
        public MainViewModel(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            // Subscribe to connection events
            _connectionManager.ConnectionAdded += OnConnectionAdded;
            _connectionManager.ConnectionRemoved += OnConnectionRemoved;
            _connectionManager.ConnectionStateChanged += OnConnectionStateChanged;

            // Add any existing connections
            foreach (var connection in _connectionManager.GetConnections())
            {
                AddServer(connection);
            }
        }

        /// <summary>
        /// Adds a new database connection
        /// </summary>
        /// <param name="window">The parent window</param>
        public async Task AddConnectionAsync(Window window)
        {
            try
            {
                var dialog = new Views.ConnectionDialog();
                var result = await dialog.ShowDialog<bool>(window);

                if (result && dialog.ConnectionInfo != null)
                {
                    var connection = _connectionManager.AddConnection(dialog.ConnectionInfo);
                    if (connection != null)
                    {
                        // Auto-connect to the new server
                        await _connectionManager.ConnectAsync(connection.ConnectionInfo.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a database connection
        /// </summary>
        /// <param name="connectionName">The connection name</param>
        public void RemoveConnection(string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                return;

            _connectionManager.RemoveConnection(connectionName);
        }

        /// <summary>
        /// Refreshes the selected connection
        /// </summary>
        public async Task RefreshConnectionAsync()
        {
            if (SelectedConnection == null)
                return;

            try
            {
                // Refresh databases
                var serverViewModel = Servers.FirstOrDefault(s => s.Connection == SelectedConnection);
                if (serverViewModel != null)
                {
                    await serverViewModel.RefreshDatabasesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the tables for the selected database
        /// </summary>
        private async Task LoadTablesAsync()
        {
            Tables.Clear();

            if (SelectedConnection == null || string.IsNullOrWhiteSpace(SelectedDatabase))
                return;

            try
            {
                var tables = await SelectedConnection.GetTablesAsync(SelectedDatabase);

                foreach (var table in tables)
                {
                    Tables.Add(table.Name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tables: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the ConnectionAdded event
        /// </summary>
        private void OnConnectionAdded(object sender, ConnectionEventArgs e)
        {
            if (e.Connection != null)
            {
                AddServer(e.Connection);
            }
        }

        /// <summary>
        /// Handles the ConnectionRemoved event
        /// </summary>
        private void OnConnectionRemoved(object sender, ConnectionEventArgs e)
        {
            if (e.Connection != null)
            {
                var server = Servers.FirstOrDefault(s => s.Connection == e.Connection);
                if (server != null)
                {
                    Servers.Remove(server);
                }

                if (SelectedConnection == e.Connection)
                {
                    SelectedConnection = null;
                }
            }
        }

        /// <summary>
        /// Handles the ConnectionStateChanged event
        /// </summary>
        private void OnConnectionStateChanged(object sender, ConnectionEventArgs e)
        {
            if (e.Connection != null)
            {
                var server = Servers.FirstOrDefault(s => s.Connection == e.Connection);
                if (server != null)
                {
                    server.IsConnected = e.Connection.IsConnected;

                    if (e.Connection.IsConnected)
                    {
                        // Refresh databases
                        server.RefreshDatabasesAsync().ContinueWith(_ => { });
                    }
                    else
                    {
                        server.Databases.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Adds a server to the collection
        /// </summary>
        private void AddServer(IDbConnection connection)
        {
            if (connection == null)
                return;

            var server = new ServerViewModel(connection);
            Servers.Add(server);

            if (connection.IsConnected)
            {
                // Refresh databases
                server.RefreshDatabasesAsync().ContinueWith(_ => { });
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for a database server
    /// </summary>
    public class ServerViewModel : INotifyPropertyChanged
    {
        private bool _isConnected;
        private bool _isExpanded;

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the database connection
        /// </summary>
        public IDbConnection Connection { get; }

        /// <summary>
        /// Gets the server name
        /// </summary>
        public string Name => Connection?.ConnectionInfo?.Name ?? "Unknown Server";

        /// <summary>
        /// Gets or sets whether the server is connected
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the server node is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    if (value && IsConnected)
                    {
                        RefreshDatabasesAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the collection of databases
        /// </summary>
        public ObservableCollection<DatabaseViewModel> Databases { get; } = new ObservableCollection<DatabaseViewModel>();

        /// <summary>
        /// Initializes a new instance of the ServerViewModel class
        /// </summary>
        /// <param name="connection">The database connection</param>
        public ServerViewModel(IDbConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            IsConnected = connection.IsConnected;
        }

        /// <summary>
        /// Refreshes the list of databases
        /// </summary>
        public async Task RefreshDatabasesAsync()
        {
            Databases.Clear();

            if (!IsConnected)
                return;

            try
            {
                var databases = await Connection.GetDatabasesAsync();

                foreach (var database in databases)
                {
                    Databases.Add(new DatabaseViewModel
                    {
                        Name = database.Name,
                        Server = this
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing databases: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for a database
    /// </summary>
    public class DatabaseViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the database name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the parent server
        /// </summary>
        public ServerViewModel Server { get; set; }

        /// <summary>
        /// Gets or sets whether the database node is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    if (value)
                    {
                        RefreshTablesAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the collection of tables
        /// </summary>
        public ObservableCollection<TableViewModel> Tables { get; } = new ObservableCollection<TableViewModel>();

        /// <summary>
        /// Refreshes the list of tables
        /// </summary>
        public async Task RefreshTablesAsync()
        {
            Tables.Clear();

            if (Server?.Connection == null)
                return;

            try
            {
                var tables = await Server.Connection.GetTablesAsync(Name);

                foreach (var table in tables)
                {
                    Tables.Add(new TableViewModel
                    {
                        Name = table.Name,
                        Database = this
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing tables: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for a database table
    /// </summary>
    public class TableViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;

        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the table name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the parent database
        /// </summary>
        public DatabaseViewModel Database { get; set; }

        /// <summary>
        /// Gets or sets whether the table node is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    if (value)
                    {
                        RefreshColumnsAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the collection of columns
        /// </summary>
        public ObservableCollection<ColumnViewModel> Columns { get; } = new ObservableCollection<ColumnViewModel>();

        /// <summary>
        /// Refreshes the list of columns
        /// </summary>
        public async Task RefreshColumnsAsync()
        {
            Columns.Clear();

            if (Database?.Server?.Connection == null)
                return;

            try
            {
                var columns = await Database.Server.Connection.GetColumnsAsync(Database.Name, Name);

                foreach (var column in columns)
                {
                    Columns.Add(new ColumnViewModel
                    {
                        Name = column.Name,
                        DataType = column.DataType,
                        IsPrimaryKey = column.IsPrimaryKey,
                        IsNullable = column.IsNullable,
                        Table = this
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing columns: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for a database column
    /// </summary>
    public class ColumnViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Event raised when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

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
        /// Gets or sets the parent table
        /// </summary>
        public TableViewModel Table { get; set; }

        /// <summary>
        /// Gets the display text for the column
        /// </summary>
        public string DisplayText => $"{Name} ({DataType}){(IsPrimaryKey ? " PK" : "")}{(IsNullable ? " NULL" : "")}";

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}