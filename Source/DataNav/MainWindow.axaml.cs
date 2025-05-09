using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using DataNav.Services;
using DataNav.ViewModels;
using System;

namespace DataNav
{
    public partial class MainWindow : Window
    {
        private readonly ConnectionManager _connectionManager;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            _connectionManager = new ConnectionManager();
            _viewModel = new MainViewModel(_connectionManager);

            // Set up event handlers
            this.FindControl<Button>("AddConnectionButton").Click += OnAddConnectionClick;
            this.FindControl<Button>("RefreshButton").Click += OnRefreshClick;
            this.FindControl<Button>("SettingsButton").Click += OnSettingsClick;

            // Set up the tree view
            SetupDatabaseTreeView();
        }

        private void SetupDatabaseTreeView()
        {
            var treeView = this.FindControl<TreeView>("DatabaseTreeView");

            // Create and add the root item programmatically
            var rootItem = new TreeViewItem { Header = "Servers", IsExpanded = true };
            treeView.Items.Add(rootItem);

            // Monitor the Servers collection in the view model
            _viewModel.Servers.CollectionChanged += (sender, e) =>
            {
                // Update the tree view when the collection changes
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateDatabaseTreeView(rootItem);
                });
            };

            // Handle selection changed
            treeView.SelectionChanged += OnTreeViewSelectionChanged;
        }

        private void UpdateDatabaseTreeView(TreeViewItem rootItem)
        {
            // Clear existing server items
            while (rootItem.Items.Count > 0)
            {
                ((TreeViewItem)rootItem.Items[0]).Items.Clear();
                rootItem.Items.RemoveAt(0);
            }

            // Add servers from the view model
            foreach (var server in _viewModel.Servers)
            {
                var serverItem = new TreeViewItem
                {
                    Header = server.Name,
                    Tag = server,
                    IsExpanded = true
                };

                rootItem.Items.Add(serverItem);

                // Add databases node
                var databasesItem = new TreeViewItem
                {
                    Header = "Databases",
                    IsExpanded = true
                };

                serverItem.Items.Add(databasesItem);

                foreach (var database in server.Databases)
                {
                    var databaseItem = new TreeViewItem
                    {
                        Header = database.Name,
                        Tag = database,
                        IsExpanded = true
                    };

                    databasesItem.Items.Add(databaseItem);

                    // Add tables
                    foreach (var table in database.Tables)
                    {
                        var tableItem = new TreeViewItem
                        {
                            Header = table.Name,
                            Tag = table
                        };

                        databaseItem.Items.Add(tableItem);

                        // Add columns
                        foreach (var column in table.Columns)
                        {
                            var columnItem = new TreeViewItem
                            {
                                Header = column.DisplayText,
                                Tag = column
                            };

                            tableItem.Items.Add(columnItem);
                        }
                    }
                }
            }
        }

        private async void OnAddConnectionClick(object sender, RoutedEventArgs e)
        {
            await _viewModel.AddConnectionAsync(this);
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshConnectionAsync();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            // TODO: Show settings dialog
            Console.WriteLine("Settings clicked");
        }

        private void OnTreeViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is TreeViewItem selectedItem && selectedItem.Tag is TableViewModel tableViewModel)
                {
                    // Open table in a new tab
                    OpenTableTab(tableViewModel);
                }
            }
        }

        private async void OpenTableTab(TableViewModel tableViewModel)
        {
            if (tableViewModel == null)
                return;

            string tabId = $"{tableViewModel.Database.Server.Name}_{tableViewModel.Database.Name}_{tableViewModel.Name}";

            // Look for existing tab
            TabControl tabControl = this.FindControl<TabControl>("ContentTabControl");

            foreach (TabItem tab in tabControl.Items)
            {
                if (tab.Tag?.ToString() == tabId)
                {
                    // Select existing tab
                    tabControl.SelectedItem = tab;
                    return;
                }
            }

            // Create new tab
            TabItem newTab = new TabItem
            {
                Header = tableViewModel.Name,
                Tag = tabId
            };

            // Add it to the TabControl
            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;

            // Create inner tab control
            var innerTabControl = new TabControl();

            // Data tab
            var dataTab = new TabItem { Header = "Data" };
            innerTabControl.Items.Add(dataTab);

            var dataGrid = new Grid();
            dataTab.Content = dataGrid;

            // Add rows
            dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dataGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Add toolbar
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Avalonia.Thickness(0, 0, 0, 5)
            };

            var executeButton = new Button
            {
                Content = "Execute Query",
                Margin = new Avalonia.Thickness(0, 0, 5, 0)
            };

            var exportButton = new Button
            {
                Content = "Export Data",
                Margin = new Avalonia.Thickness(0, 0, 5, 0)
            };

            var filterButton = new Button
            {
                Content = "Filter",
                Margin = new Avalonia.Thickness(0, 0, 5, 0)
            };

            toolbar.Children.Add(executeButton);
            toolbar.Children.Add(exportButton);
            toolbar.Children.Add(filterButton);

            Grid.SetRow(toolbar, 0);
            dataGrid.Children.Add(toolbar);

            // Add data grid
            var grid = new DataGrid { AutoGenerateColumns = true };
            Grid.SetRow(grid, 1);
            dataGrid.Children.Add(grid);

            // Structure tab
            var structureTab = new TabItem { Header = "Structure" };
            innerTabControl.Items.Add(structureTab);

            var columnsList = new ListBox();
            structureTab.Content = columnsList;

            // Load columns
            try
            {
                var columns = await tableViewModel.Database.Server.Connection.GetColumnsAsync(
                    tableViewModel.Database.Name,
                    tableViewModel.Name);

                foreach (var column in columns)
                {
                    string text = $"{column.Name} ({column.DataType})";
                    if (column.IsPrimaryKey)
                        text += " - Primary Key";
                    if (!column.IsNullable)
                        text += " - NOT NULL";

                    columnsList.Items.Add(new ListBoxItem { Content = text });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading columns: {ex.Message}");
            }

            // Query tab
            var queryTab = new TabItem { Header = "Query" };
            innerTabControl.Items.Add(queryTab);

            var queryGrid = new Grid();
            queryTab.Content = queryGrid;

            // Add rows
            queryGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            queryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            queryGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

            // Add query text box
            var queryTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                FontFamily = new Avalonia.Media.FontFamily("Consolas, Menlo, Monospace"),
                Text = $"SELECT * FROM {tableViewModel.Database.Name}.{tableViewModel.Name} LIMIT 10;"
            };
            Grid.SetRow(queryTextBox, 0);
            queryGrid.Children.Add(queryTextBox);

            // Add buttons
            var queryToolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Avalonia.Thickness(0, 5, 0, 5)
            };

            var executeQueryButton = new Button
            {
                Content = "Execute",
                Margin = new Avalonia.Thickness(0, 0, 5, 0)
            };

            queryToolbar.Children.Add(executeQueryButton);
            Grid.SetRow(queryToolbar, 1);
            queryGrid.Children.Add(queryToolbar);

            // Add results grid
            var resultsGrid = new DataGrid { AutoGenerateColumns = true };
            Grid.SetRow(resultsGrid, 2);
            queryGrid.Children.Add(resultsGrid);

            // Set up execute query event
            executeQueryButton.Click += async (s, e) =>
            {
                try
                {
                    var query = queryTextBox.Text;
                    var result = await tableViewModel.Database.Server.Connection.ExecuteQueryAsync(query);

                    // Convert to a format the data grid can display
                    foreach (var row in result.Rows)
                    {
                        //resultsGrid.Items.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing query: {ex.Message}");
                }
            };

            // Set the tab content
            newTab.Content = innerTabControl;

            // Load data
            try
            {
                var connection = tableViewModel.Database.Server.Connection;
                var result = await connection.ExecuteQueryAsync(
                    $"SELECT * FROM {tableViewModel.Database.Name}.{tableViewModel.Name} LIMIT 100;");

                foreach (var row in result.Rows)
                {
                    //grid.Items.Add(row);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading table data: {ex.Message}");
            }
        }
    }
}