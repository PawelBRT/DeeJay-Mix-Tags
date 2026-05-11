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

        public void WriteRow(string filePath, string beforeGenre, string beforeLabel, string afterGenre, string afterLabel)
        {
            var obj = new
            {
                file = filePath,
                timestamp = DateTimeOffset.Now.ToString("o"),
                before = new { genre = beforeGenre, label = beforeLabel },
                after = new { genre = afterGenre, label = afterLabel }
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
