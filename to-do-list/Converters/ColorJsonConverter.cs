using System;
using System.Text.Json;
using System.Windows.Media;

namespace EasyToDo.Converters
{
    public class ColorJsonConverter : System.Text.Json.Serialization.JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var colorString = reader.GetString();
            return (Color)ColorConverter.ConvertFromString(colorString);
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}