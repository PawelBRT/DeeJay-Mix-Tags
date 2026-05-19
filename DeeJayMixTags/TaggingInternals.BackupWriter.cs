using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using IOFile = System.IO.File;

namespace Mp3TaggerGUI
{
    internal sealed class TaggingBackupWriter : IDisposable
    {
        public string Path { get; }

        private readonly StreamWriter _sw;
        private bool _isFirst = true;

        public TaggingBackupWriter(string path)
        {
            Path = path;
            _sw = new StreamWriter(path, false, new UTF8Encoding(true));
            _sw.WriteLine("[");
        }

        public void WriteRow(ChangeRecord r, string filePath)
        {
            var obj = new
            {
                file = filePath,
                timestamp = DateTimeOffset.Now.ToString("o"),
                changedFields = r.ChangedFields,
                before = new { genre = r.BeforeGenre, label = r.BeforeLabel, comment = r.BeforeComment, txxx = r.BeforeTxxx },
                after = new { genre = r.AfterGenre, label = r.AfterLabel, comment = r.AfterComment, txxx = r.AfterTxxx }
            };

            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            if (!_isFirst)
                _sw.WriteLine(",");

            _sw.Write(json);
            _isFirst = false;
        }

        public void Dispose()
        {
            _sw.WriteLine();
            _sw.WriteLine("]");
            _sw.Dispose();
        }
    }
}
