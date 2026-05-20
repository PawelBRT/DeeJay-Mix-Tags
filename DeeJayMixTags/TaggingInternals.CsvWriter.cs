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
            _sw = new StreamWriter(path, false, new UTF8Encoding(true)) { AutoFlush = true };
            _sw.WriteLine("File;Status;ChangedFields;GenresBefore;GenresAfter;LabelBefore;LabelAfter;CommentBefore;CommentAfter;TxxxBefore;TxxxAfter");
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
            _sw.WriteLine($"{Esc(System.IO.Path.GetFileName(path))};{Esc(status)};{Esc(r.ChangedFields)};{Esc(r.BeforeGenre)};{Esc(r.AfterGenre)};{Esc(r.BeforeLabel)};{Esc(r.AfterLabel)};{Esc(r.BeforeComment)};{Esc(r.AfterComment)};{Esc(r.BeforeTxxx)};{Esc(r.AfterTxxx)}");
        }

        public void WriteError(string path, string message)
        {
            _sw.WriteLine($"{Esc(System.IO.Path.GetFileName(path))};ERROR;{Esc(message)};;;;;;;;");
        }

        public void Dispose() => _sw.Dispose();
    }
}
