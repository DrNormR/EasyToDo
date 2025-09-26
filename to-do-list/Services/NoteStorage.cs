using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using to_do_list.Converters;
using to_do_list.Models;

namespace to_do_list.Services
{
    public static class NoteStorage
    {
        private static readonly string SaveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ToDoList",
            "notes.json"
        );

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        public static void SaveNotes(ObservableCollection<Note> notes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                string jsonString = JsonSerializer.Serialize(notes, Options);
                File.WriteAllText(SaveFilePath, jsonString);
            }
            catch (Exception ex)
            {
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
}