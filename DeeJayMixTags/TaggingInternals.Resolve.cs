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
            Dictionary<(string a, string t, string v), TrackLookupInfo> db,
            bool allowFilenameFallback,
            out TrackLookupInfo info)
        {
            if (db.TryGetValue((artist, title, version), out var exact) && exact is not null)
            {
                info = exact;
                return true;
            }

            if (db.TryGetValue((artist, title, ""), out var versionless) && versionless is not null)
            {
                info = versionless;
                return true;
            }

            if (!allowFilenameFallback)
            {
                info = new TrackLookupInfo();
                return false;
            }

            return TryGetInfoFromFilename(path, db, out info);
        }

        private static bool TryGetInfoFromFilename(
            string path,
            Dictionary<(string a, string t, string v), TrackLookupInfo> db,
            out TrackLookupInfo info)
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            // Usuwa sufiks identyfikatora (np. "_a1b2c3" dopinany przez serwisy promo).
            var trimmed = Regex.Replace(baseName, "_\\w{1,12}$", "");

            var withVersion = Regex.Match(trimmed, @"^(.*?)\s*-\s*(.*?)\s*\(([^()]*)\)\s*$");
            if (withVersion.Success)
            {
                var a2 = TaggingText.Norm(withVersion.Groups[1].Value);
                var t2 = TaggingText.Norm(withVersion.Groups[2].Value);
                var v2 = TaggingText.Norm(withVersion.Groups[3].Value);

                if (db.TryGetValue((a2, t2, v2), out var exact) && exact is not null)
                {
                    info = exact;
                    return true;
                }

                if (db.TryGetValue((a2, t2, ""), out var versionless) && versionless is not null)
                {
                    info = versionless;
                    return true;
                }

                info = new TrackLookupInfo();
                return false;
            }

            var simple = Regex.Match(trimmed, @"^(.*?)\s*-\s*(.*?)\s*$");
            if (simple.Success)
            {
                var a2 = TaggingText.Norm(simple.Groups[1].Value);
                var t2 = TaggingText.Norm(simple.Groups[2].Value);
                if (db.TryGetValue((a2, t2, ""), out var versionless) && versionless is not null)
                {
                    info = versionless;
                    return true;
                }
            }

            info = new TrackLookupInfo();
            return false;
        }
    }
}
