using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Mp3TaggerGUI
{
    internal static class TaggingResolver
    {
        public static bool TryResolveInfo(
            string path,
            string artist,
            string title,
            string version,
            Dictionary<(string a, string t, string v), (string genres, string labels)> db,
            bool allowFilenameFallback,
            out (string genres, string labels) info)
        {
            if (db.TryGetValue((artist, title, version), out info) || db.TryGetValue((artist, title, ""), out info))
                return true;

            if (!allowFilenameFallback)
                return false;

            return TryGetInfoFromFilename(path, db, out info);
        }

        private static bool TryGetInfoFromFilename(
            string path,
            Dictionary<(string a, string t, string v), (string genres, string labels)> db,
            out (string genres, string labels) info)
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            var trimmed = Regex.Replace(baseName, "_\\w{1,12}$", "");

            var withVersion = Regex.Match(trimmed, @"^(.*?)\s*-\s*(.*?)\s*\(([^()]*)\)\s*$");
            if (withVersion.Success)
            {
                var a2 = TaggingText.Norm(withVersion.Groups[1].Value);
                var t2 = TaggingText.Norm(withVersion.Groups[2].Value);
                var v2 = TaggingText.Norm(withVersion.Groups[3].Value);

                if (db.TryGetValue((a2, t2, v2), out info))
                    return true;

                return db.TryGetValue((a2, t2, ""), out info);
            }

            var simple = Regex.Match(trimmed, @"^(.*?)\s*-\s*(.*?)\s*$");
            if (simple.Success)
            {
                var a2 = TaggingText.Norm(simple.Groups[1].Value);
                var t2 = TaggingText.Norm(simple.Groups[2].Value);
                return db.TryGetValue((a2, t2, ""), out info);
            }

            info = default;
            return false;
        }
    }
}
