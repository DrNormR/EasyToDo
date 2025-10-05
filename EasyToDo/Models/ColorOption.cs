using System.Windows.Media;

namespace EasyToDo.Models
{
    public class ColorOption
    {
        public string Name { get; set; }
        public SolidColorBrush Color { get; set; }
        public Color ColorValue { get; set; }

        public ColorOption(string name, string hexColor)
        {
            Name = name;
            ColorValue = (Color)ColorConverter.ConvertFromString(hexColor);
            Color = new SolidColorBrush(ColorValue);
        }
    }
}