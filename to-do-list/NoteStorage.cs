using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace to_do_list
{
    public static class NoteStorage
    {
        private static readonly string SaveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ToDoList",
            "notes.json"
        );

        // JsonSerializerOptions setup for color serialization
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        public static void SaveNotes(ObservableCollection<Note> notes)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));

                string jsonString = JsonSerializer.Serialize(notes, Options);
                File.WriteAllText(SaveFilePath, jsonString);
            }
            catch (Exception ex)
            {
                // In a production app, you might want to log this error
                System.Diagnostics.Debug.WriteLine($"Error saving notes: {ex.Message}");
            }
        }

        public static ObservableCollection<Note> LoadNotes()
        {
            if (File.Exists(SaveFilePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(SaveFilePath);
                    var notes = JsonSerializer.Deserialize<ObservableCollection<Note>>(jsonString, Options);
                    return notes ?? new ObservableCollection<Note>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
                }
            }

            return new ObservableCollection<Note>();
        }
    }

    // Custom JSON converter for System.Windows.Media.Color
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
