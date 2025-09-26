using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;

namespace to_do_list
{
    public partial class NoteWindow : Window
    {
        private readonly Note _note;
        private bool _isPinned = false;

        public NoteWindow(Note note)
        {
            InitializeComponent();
            _note = note;
            DataContext = _note;

            // Set initial color
            SetBackgroundColor(_note.BackgroundColor);
            ColorPicker.SelectedIndex = 0; // Default to yellow

            // Set window position slightly offset from cursor
            var mousePosition = Mouse.GetPosition(Application.Current.MainWindow);
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var point = mainWindow.PointToScreen(mousePosition);
                Left = point.X + 20;
                Top = point.Y + 20;
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            Topmost = _isPinned;
            if (sender is Button pinButton)
            {
                pinButton.Content = _isPinned ? "📍" : "📌";
                pinButton.ToolTip = _isPinned ? "Unpin window" : "Pin window on top";
            }
        }

        private void NewItemTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text) && 
                    textBox.Text != "Type here to add a new item...")
                {
                    _note.Items.Add(new NoteItem { Text = textBox.Text, IsChecked = false });
                    textBox.Text = string.Empty;
                    e.Handled = true; // Prevent the beep sound
                }
            }
        }

        private void NewItemTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text == "Type here to add a new item...")
            {
                textBox.Text = string.Empty;
                textBox.FontStyle = FontStyles.Normal;
            }
        }

        private void NewItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Type here to add a new item...";
                textBox.FontStyle = FontStyles.Italic;
            }
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPicker.SelectedItem is ComboBoxItem selectedItem)
            {
                var colorHex = selectedItem.Tag.ToString();
                if (colorHex != null)
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    _note.BackgroundColor = color;
                    SetBackgroundColor(color);
                }
            }
        }

        private void SetBackgroundColor(Color color)
        {
            Background = new SolidColorBrush(color);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            // The checkbox state is automatically updated through binding
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is NoteItem item)
            {
                _note.Items.Remove(item);
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && sender is TextBlock textBlock)
            {
                var parent = textBlock.Parent as Grid;
                if (parent != null)
                {
                    var textBox = parent.Children.OfType<TextBox>().FirstOrDefault();
                    if (textBox != null)
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                        textBox.Visibility = Visibility.Visible;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var parent = textBox.Parent as Grid;
                if (parent != null)
                {
                    var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBlock != null)
                    {
                        textBox.Visibility = Visibility.Collapsed;
                        textBlock.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (e.Key == Key.Enter)
                {
                    var parent = textBox.Parent as Grid;
                    if (parent != null)
                    {
                        var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBox.Visibility = Visibility.Collapsed;
                            textBlock.Visibility = Visibility.Visible;
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (textBox.DataContext is NoteItem item)
                    {
                        // Revert changes
                        textBox.Text = item.Text;
                    }
                    var parent = textBox.Parent as Grid;
                    if (parent != null)
                    {
                        var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBox.Visibility = Visibility.Collapsed;
                            textBlock.Visibility = Visibility.Visible;
                        }
                    }
                    e.Handled = true;
                }
            }
        }
    }
}