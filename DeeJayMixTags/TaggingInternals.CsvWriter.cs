using System;
using System.IO;
using System.Text;

namespace Mp3TaggerGUI
{
    internal sealed class TaggingCsvWriter : IDisposable
    {
        public string Path { get; }
        private readonly StreamWriter _sw;

        public TaggingCsvWriter(string path)
        {
            Path = path;
            _sw = new StreamWriter(path, false, new UTF8Encoding(true));
            _sw.WriteLine("File;Status;GenresBefore;GenresAfter;LabelBefore;LabelAfter");
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Contains(';') || s.Contains('"')) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public void WriteRow(ChangeRecord r, string path)
        {
            string status = r.Kind.ToString().ToUpperInvariant();
            _sw.WriteLine($"{Esc(System.IO.Path.GetFileName(path))};{Esc(status)};{Esc(r.BeforeGenre)};{Esc(r.AfterGenre)};{Esc(r.BeforeLabel)};{Esc(r.AfterLabel)}");
        }

        public void WriteError(string path, string message)
        {
            _sw.WriteLine($"{Esc(System.IO.Path.GetFileName(path))};ERROR;;;{Esc(message)};");
        }

        public void Dispose() => _sw.Dispose();
    }
}
