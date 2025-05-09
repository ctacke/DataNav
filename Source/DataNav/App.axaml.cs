using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataNav.Services;

namespace DataNav
{
    public partial class App : Application
    {
        /// <summary>
        /// Gets the connection manager instance
        /// </summary>
        public static ConnectionManager ConnectionManager { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Initialize services
            ConnectionManager = new ConnectionManager();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();

                // Handle application exit
                desktop.ShutdownRequested += (s, e) =>
                {
                    // Clean up connections
                    foreach (var connection in ConnectionManager.GetConnections())
                    {
                        try
                        {
                            connection.DisconnectAsync().Wait();
                            connection.Dispose();
                        }
                        catch
                        {
                            // Ignore errors during shutdown
                        }
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}