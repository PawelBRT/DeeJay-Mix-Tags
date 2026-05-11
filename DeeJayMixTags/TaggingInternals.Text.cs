using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mp3TaggerGUI
{
    internal static class TaggingText
    {
        public const string Sep = " | ";
        public const string AlwaysAppendToken = "DJPromo.pl";

        private static readonly HashSet<string> DropInGenre = new(StringComparer.OrdinalIgnoreCase)
        {
            "Świat", "Swiat", "Polska"
        };

        private static readonly CultureInfo Pl = new("pl-PL");

        public static string Norm(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s.Trim(), @"\s+", " ");

        public static List<string> SplitMulti(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return [];
            var s = Norm(raw);
            s = s.Replace("/", "|").Replace(";", "|").Replace(":", "|").Replace(",", "|");
            return s.Split('|').Select(Norm).Where(p => p.Length > 0).ToList();
        }

        public static string TitleToken(string tok, bool forcePopUpper)
        {
            if (string.IsNullOrWhiteSpace(tok)) return tok ?? "";
            if (forcePopUpper && tok.Equals("POP", StringComparison.OrdinalIgnoreCase)) return "POP";

            var words = tok.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                var head = w.Substring(0, 1).ToUpper(Pl);
                var tail = w.Length > 1 ? w.Substring(1).ToLower(Pl) : "";
                words[i] = head + tail;
            }

            return string.Join(' ', words);
        }

        public static List<string> CleanGenreList(string? raw)
        {
            var parts = SplitMulti(raw ?? "");
            var outList = new List<string>();

            foreach (var it in parts)
            {
                var it2 = Regex.Replace(it, @"^(Świat|Swiat|Polska)\s*:\s*", "", RegexOptions.IgnoreCase);
                if (DropInGenre.Contains(it2)) continue;
                it2 = TitleToken(it2, true);
                if (!string.IsNullOrWhiteSpace(it2)) outList.Add(it2);
            }

            return Dedup(outList);
        }

        public static List<string> CleanLabelList(string? raw)
        {
            var parts = SplitMulti(raw ?? "").Select(p => Regex.Replace(p, @"\.$", "").Trim()).ToList();
            parts = parts.Select(p => TitleToken(p, false)).ToList();
            return Dedup(parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToList());
        }

        public static List<string> Dedup(List<string> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var res = new List<string>();

            foreach (var x in items)
            {
                var key = (x ?? "").Trim();
                if (key.Length == 0) continue;
                if (seen.Add(key)) res.Add(key.Trim());
            }

            return res;
        }

        public static List<string> MergeValues(List<string> newer, List<string> older, bool prependNew, bool dedup)
        {
            var merged = prependNew ? newer.Concat(older).ToList() : older.Concat(newer).ToList();
            return dedup ? Dedup(merged) : merged.Select(Norm).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public static string Join(List<string> parts) =>
            string.Join(Sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Norm));

        public static (string artist, string title, string version) ExtractArtistTitleVersion(TagLib.File file)
        {
            string artist = Norm(file.Tag.JoinedPerformers ?? "");
            string tit2 = Norm(file.Tag.Title ?? "");

            string title = tit2, version = "";
            var m = Regex.Match(tit2, @"\(([^()]*)\)\s*$");
            if (m.Success)
            {
                version = Norm(m.Groups[1].Value);
                title = Norm(Regex.Replace(tit2, @"\s*\([^()]*\)\s*$", ""));
            }

            return (artist, title, version);
        }
    }
}
