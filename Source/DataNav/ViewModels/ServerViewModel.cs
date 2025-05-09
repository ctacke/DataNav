using DataNav.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DataNav.ViewModels
{
    /// <summary>
    /// ViewModel for a database server
    /// </summary>
    public class ServerViewModel : INotifyPropertyChanged
    {
        private bool _isConnected;
        private bool _isExpanded;
        private bool _isRefreshing;

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

                    if (value && IsConnected && !_isRefreshing)
                    {
                        RefreshDatabasesAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the server is currently refreshing databases
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    OnPropertyChanged();
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
            _isExpanded = true; // Auto-expand by default

            // If already connected, load databases
            if (IsConnected)
            {
                RefreshDatabasesAsync().ContinueWith(_ => { });
            }
        }

        /// <summary>
        /// Refreshes the list of databases
        /// </summary>
        public async Task RefreshDatabasesAsync()
        {
            Console.WriteLine($"RefreshDatabasesAsync called for server {Name}");

            if (IsRefreshing)
            {
                Console.WriteLine("Already refreshing, skipping");
                return;
            }

            if (!IsConnected)
            {
                Console.WriteLine("Server not connected, skipping database refresh");
                return;
            }

            try
            {
                IsRefreshing = true;

                Console.WriteLine("Getting databases from connection");
                var databases = await Connection.GetDatabasesAsync();

                // Clear on UI thread to avoid collection modified exceptions
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Databases.Clear();
                });

                foreach (var database in databases)
                {
                    Console.WriteLine($"Adding database: {database.Name}");

                    // Add on UI thread
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Databases.Add(new DatabaseViewModel
                        {
                            Name = database.Name,
                            Server = this
                        });
                    });
                }

                Console.WriteLine($"Added {Databases.Count} databases");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing databases: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsRefreshing = false;
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
}