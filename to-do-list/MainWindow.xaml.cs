using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using to_do_list;

namespace to_do_list
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Note> Notes { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            NotesListBox.ItemsSource = Notes;
            CreateNoteButton.Click += CreateNoteButton_Click;
            DeleteNoteButton.Click += DeleteNoteButton_Click;
            DuplicateNoteButton.Click += DuplicateNoteButton_Click;
            NotesListBox.MouseDoubleClick += NotesListBox_MouseDoubleClick;
        }

        private void CreateNoteButton_Click(object sender, RoutedEventArgs e)
        {
            var note = new Note { Title = $"Note {Notes.Count + 1}" };
            Notes.Add(note);
            var noteWindow = new NoteWindow(note);
            noteWindow.Show();
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
                Notes.Remove(note);
        }

        private void DuplicateNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
            {
                var copy = new Note
                {
                    Title = note.Title + " (Copy)",
                    BackgroundColor = note.BackgroundColor,
                    Items = new ObservableCollection<NoteItem>(note.Items.Select(i => new NoteItem { Text = i.Text, IsChecked = i.IsChecked }))
                };
                Notes.Add(copy);
            }
        }

        private void NotesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
            {
                var noteWindow = new NoteWindow(note);
                noteWindow.Show();
            }
        }
    }
}