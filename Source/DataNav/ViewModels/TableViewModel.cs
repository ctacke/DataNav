using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DataNav.ViewModels
{
    /// <summary>
    /// ViewModel for a database table
    /// </summary>
    public class TableViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isRefreshing;

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

                    if (value && !_isRefreshing && Database?.Server?.IsConnected == true)
                    {
                        RefreshColumnsAsync().ContinueWith(_ => { });
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the table is currently refreshing columns
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
        /// Gets the collection of columns
        /// </summary>
        public ObservableCollection<ColumnViewModel> Columns { get; } = new ObservableCollection<ColumnViewModel>();

        /// <summary>
        /// Refreshes the list of columns
        /// </summary>
        public async Task RefreshColumnsAsync()
        {
            Console.WriteLine($"RefreshColumnsAsync called for table {Database?.Name}.{Name}");

            if (IsRefreshing)
            {
                Console.WriteLine("Already refreshing, skipping");
                return;
            }

            if (Database?.Server?.Connection == null || !Database.Server.IsConnected)
            {
                Console.WriteLine("Server not connected, skipping column refresh");
                return;
            }

            try
            {
                IsRefreshing = true;

                Console.WriteLine($"Getting columns for table {Database.Name}.{Name}");
                var columns = await Database.Server.Connection.GetColumnsAsync(Database.Name, Name);

                // Clear on UI thread to avoid collection modified exceptions
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Columns.Clear();
                });

                foreach (var column in columns)
                {
                    Console.WriteLine($"Adding column: {column.Name} ({column.DataType})");

                    // Add on UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Columns.Add(new ColumnViewModel
                        {
                            Name = column.Name,
                            DataType = column.DataType,
                            IsPrimaryKey = column.IsPrimaryKey,
                            IsNullable = column.IsNullable,
                            Table = this
                        });
                    });
                }

                Console.WriteLine($"Added {Columns.Count} columns");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing columns: {ex.Message}");
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