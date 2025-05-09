using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DataNav.ViewModels
{
    /// <summary>
    /// ViewModel for a database
    /// </summary>
    public class DatabaseViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isRefreshing;

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

                    if (value && !_isRefreshing && Server?.IsConnected == true)
                    {
                        RefreshTablesAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the database is currently refreshing tables
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
        /// Gets the collection of tables
        /// </summary>
        public ObservableCollection<TableViewModel> Tables { get; } = new ObservableCollection<TableViewModel>();

        /// <summary>
        /// Refreshes the list of tables
        /// </summary>
        public async Task RefreshTablesAsync()
        {
            Console.WriteLine($"RefreshTablesAsync called for database {Name}");

            if (IsRefreshing)
            {
                Console.WriteLine("Already refreshing, skipping");
                return;
            }

            if (Server?.Connection == null || !Server.IsConnected)
            {
                Console.WriteLine("Server not connected, skipping table refresh");
                return;
            }

            try
            {
                IsRefreshing = true;

                Console.WriteLine($"Getting tables for keyspace {Name}");
                var tables = await Server.Connection.GetTablesAsync(Name);

                // Clear on UI thread to avoid collection modified exceptions
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Tables.Clear();
                });

                foreach (var table in tables)
                {
                    Console.WriteLine($"Adding table: {table.Name}");

                    // Add on UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Tables.Add(new TableViewModel
                        {
                            Name = table.Name,
                            Database = this
                        });
                    });
                }

                Console.WriteLine($"Added {Tables.Count} tables");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing tables: {ex.Message}");
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