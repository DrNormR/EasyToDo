using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using EasyToDo.Services;

namespace EasyToDo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Check for updates in the background after startup
            _ = Task.Run(async () =>
            {
                // Wait a bit to let the main window load first
                await Task.Delay(2000);
                await UpdateService.CheckForUpdatesOnStartupAsync();
            });
        }
    }
}
