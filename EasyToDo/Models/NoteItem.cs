using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasyToDo.Models
{
    public class NoteItem : INotifyPropertyChanged
    {
        private string _text;
        private bool _isChecked;
        private bool _isCritical;
        private bool _isHeading;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCritical
        {
            get => _isCritical;
            set
            {
                if (_isCritical != value)
                {
                    _isCritical = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsHeading
        {
            get => _isHeading;
            set
            {
                if (_isHeading != value)
                {
                    _isHeading = value;
                    OnPropertyChanged();
                    
                    // Reset checked and critical state for headings
                    if (_isHeading)
                    {
                        IsChecked = false;
                        IsCritical = false;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}