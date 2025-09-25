using System.Collections.ObjectModel;
using System.Windows.Media;

namespace to_do_list
{
    public class NoteItem
    {
        public string Text { get; set; }
        public bool IsChecked { get; set; }
    }

    public class Note
    {
        public string Title { get; set; }
        public ObservableCollection<NoteItem> Items { get; set; } = new();
        public Color BackgroundColor { get; set; } = Colors.Yellow;
    }
}
