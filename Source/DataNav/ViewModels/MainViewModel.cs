using Avalonia.Controls;
using DataNav.Core.Interfaces;
using DataNav.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DataNav.ViewModels;

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
            Console.WriteLine("AddConnectionAsync started");
            var dialog = new Views.ConnectionDialog();
            var result = await dialog.ShowDialog<bool>(window);

            Console.WriteLine($"Dialog result: {result}, ConnectionInfo: {(dialog.ConnectionInfo != null ? dialog.ConnectionInfo.Name : "null")}");

            if (result && dialog.ConnectionInfo != null)
            {
                Console.WriteLine($"Adding connection to manager: {dialog.ConnectionInfo.Name}");
                var connection = _connectionManager.AddConnection(dialog.ConnectionInfo);

                if (connection != null)
                {
                    // Important: Set the selected connection so we can refresh it later
                    SelectedConnection = connection;

                    Console.WriteLine($"Connection added, auto-connecting to: {connection.ConnectionInfo.Name}");
                    // Auto-connect to the new server
                    var connected = await _connectionManager.ConnectAsync(connection.ConnectionInfo.Name);

                    if (connected)
                    {
                        Console.WriteLine("Connection successful, forcing database refresh");
                        // Force refresh databases for this connection
                        var server = Servers.FirstOrDefault(s => s.Connection == connection);
                        if (server != null)
                        {
                            await server.RefreshDatabasesAsync();
                            Console.WriteLine($"Database refresh completed, found {server.Databases.Count} databases");
                        }
                        else
                        {
                            Console.WriteLine("Server not found in Servers collection");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Connection failed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding connection: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
