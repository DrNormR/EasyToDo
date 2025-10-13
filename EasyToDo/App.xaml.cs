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
            
            // Removed automatic update check on startup
            // Users can manually check for updates using the "Check for Updates" button
        }
    }
}
