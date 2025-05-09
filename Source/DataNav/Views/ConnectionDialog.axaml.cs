using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DataNav.Core;
using DataNav.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace DataNav.Views;

public partial class ConnectionDialog : Window
{
    public ConnectionInfo ConnectionInfo { get; private set; }

    public ConnectionDialog()
    {
        InitializeComponent();

        // Set up event handlers
        this.FindControl<Button>("TestButton").Click += OnTestButtonClick;
        this.FindControl<Button>("SaveButton").Click += OnSaveButtonClick;
        this.FindControl<Button>("CancelButton").Click += OnCancelButtonClick;

        // Set default values
        this.FindControl<TextBox>("HostTextBox").Text = "localhost";
        this.FindControl<TextBox>("PortTextBox").Text = "9042"; // Default Cassandra port
        this.FindControl<ComboBox>("ProviderTypeComboBox").SelectedIndex = 0; // Default to Cassandra
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnTestButtonClick(object sender, RoutedEventArgs e)
    {
        // Get connection info from form
        var connectionInfo = GetConnectionInfoFromForm();
        if (connectionInfo == null)
            return;

        // Disable buttons during test
        this.FindControl<Button>("TestButton").IsEnabled = false;
        this.FindControl<Button>("SaveButton").IsEnabled = false;

        // Test connection
        TestConnection(connectionInfo).ContinueWith(task =>
        {
            // Re-enable buttons
            Dispatcher.UIThread.Post(() =>
            {
                this.FindControl<Button>("TestButton").IsEnabled = true;
                this.FindControl<Button>("SaveButton").IsEnabled = true;

                if (task.Result)
                {
                    // Show success message
                    ShowMessageBox("Connection Test", "Connection successful!");
                }
                else
                {
                    // Show error message
                    ShowMessageBox("Connection Test", "Connection failed. Please check your settings.");
                }
            });
        });
    }

    private void OnSaveButtonClick(object sender, RoutedEventArgs e)
    {
        // Get connection info from form
        var connectionInfo = GetConnectionInfoFromForm();
        if (connectionInfo == null)
            return;

        // Set result and close dialog
        ConnectionInfo = connectionInfo;
        Close(true);
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        // Close dialog without saving
        Close(false);
    }

    private ConnectionInfo GetConnectionInfoFromForm()
    {
        try
        {
            // Get values from controls
            var name = this.FindControl<TextBox>("ConnectionNameTextBox").Text?.Trim();
            var host = this.FindControl<TextBox>("HostTextBox").Text?.Trim();
            var portText = this.FindControl<TextBox>("PortTextBox").Text?.Trim();
            var username = this.FindControl<TextBox>("UsernameTextBox").Text?.Trim();
            var password = this.FindControl<TextBox>("PasswordTextBox").Text;
            var useSsl = this.FindControl<CheckBox>("SslCheckBox").IsChecked ?? false;

            // Get selected provider type
            var providerTypeIndex = this.FindControl<ComboBox>("ProviderTypeComboBox").SelectedIndex;
            string providerType;
            switch (providerTypeIndex)
            {
                case 0:
                    providerType = "cassandra";
                    break;
                case 1:
                    providerType = "postgresql";
                    break;
                case 2:
                    providerType = "sqlserver";
                    break;
                case 3:
                    providerType = "mysql";
                    break;
                default:
                    providerType = "cassandra";
                    break;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowMessageBox("Validation Error", "Connection name is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                ShowMessageBox("Validation Error", "Hostname is required.");
                return null;
            }

            // Parse port
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
            {
                ShowMessageBox("Validation Error", "Port must be a valid number between 1 and 65535.");
                return null;
            }

            // Create connection info
            var connectionInfo = new ConnectionInfo
            {
                Name = name,
                ProviderType = providerType,
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                UseSsl = useSsl
            };

            // Add advanced options
            var timeoutText = this.FindControl<TextBox>("TimeoutTextBox").Text?.Trim();
            if (int.TryParse(timeoutText, out int timeout) && timeout > 0)
            {
                connectionInfo.Options["Timeout"] = timeout.ToString();
            }

            return connectionInfo;
        }
        catch (Exception ex)
        {
            ShowMessageBox("Error", $"An error occurred: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> TestConnection(ConnectionInfo connectionInfo)
    {
        try
        {
            // Create appropriate provider
            IDbConnection connection = null;
            switch (connectionInfo.ProviderType.ToLowerInvariant())
            {
                case "cassandra":
                    connection = new Providers.Cassandra.CassandraProvider(connectionInfo);
                    break;
                    // Add other provider types here as they are implemented
            }

            if (connection == null)
                return false;

            // Test connection
            using (connection)
            {
                return await connection.ConnectAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error testing connection: {ex.Message}");
            return false;
        }
    }

    private async void ShowMessageBox(string title, string message)
    {
        var messageBox = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Width = 350,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 15)
        };
        Grid.SetRow(textBlock, 0);

        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Width = 80
        };
        Grid.SetRow(button, 1);

        button.Click += (s, e) => messageBox.Close();

        grid.Children.Add(textBlock);
        grid.Children.Add(button);

        messageBox.Content = grid;
        await messageBox.ShowDialog(this);
    }
}