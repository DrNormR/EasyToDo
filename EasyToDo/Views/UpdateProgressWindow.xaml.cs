using System.Windows;

namespace EasyToDo.Services
{
    public partial class UpdateProgressWindow : Window
    {
        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBar.Value = percentage;
            });
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
            });
        }
    }
}