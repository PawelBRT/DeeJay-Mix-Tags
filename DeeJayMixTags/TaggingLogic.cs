// TaggingLogic.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using TagLib;
using IOFile = System.IO.File;

namespace Mp3TaggerGUI
{
    public static class TaggingLogic
    {
        public sealed class Result
        {
            public int Total { get; set; }
            public int Updated { get; set; }
            public int Unchanged { get; set; }
            public int Missing { get; set; }
            public int Errors { get; set; }
            public string? CsvPath { get; set; }
        }

        public static Result Process(
            string mp3Root,
            string jsonPath,
            TaggingOptions flags,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            var files = Directory.EnumerateFiles(mp3Root, "*.mp3", SearchOption.AllDirectories).ToList();
            return ProcessCore(mp3Root, jsonPath, flags, files, TagLib.File.Create, onTotal, onStep, onLog, cancellationToken);
        }

        internal static Result ProcessCore(
            string reportRoot,
            string jsonPath,
            TaggingOptions flags,
            IEnumerable<string> files,
            Func<string, TagLib.File>? fileFactory = null,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            var db = LoadDatabase(jsonPath);
            var fileList = files.ToList();
            onTotal?.Invoke(fileList.Count);

            var openFile = fileFactory ?? TagLib.File.Create;
            var res = new Result { Total = fileList.Count };
            var csv = flags.WriteCsvReport ? new TaggingCsvWriter(Path.Combine(reportRoot, "_tagger_report.csv")) : null;
            var backup = (!flags.DryRun && flags.WritePerFileBackup)
                ? new TaggingBackupWriter(Path.Combine(reportRoot, $"_tagger_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"))
                : null;

            foreach (var path in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var r = ProcessOneWithRetry(path, db, flags, onLog, openFile, cancellationToken);
                    switch (r.Kind)
                    {
                        case ChangeKind.Updated: res.Updated++; break;
                        case ChangeKind.Unchanged: res.Unchanged++; break;
                        case ChangeKind.Missing: res.Missing++; break;
                    }

                    csv?.WriteRow(r, path);
                    if (r.Kind == ChangeKind.Updated)
                        backup?.WriteRow(path, r.BeforeGenre, r.BeforeLabel, r.AfterGenre, r.AfterLabel);
                }
                catch (Exception ex)
                {
                    res.Errors++;
                    onLog?.Invoke($"ERROR: {Path.GetFileName(path)} -> {ex.Message}");
                    csv?.WriteError(path, ex.Message);
                }
                finally { onStep?.Invoke(); }
            }

            onLog?.Invoke("");
            onLog?.Invoke("--- PODSUMOWANIE ---");
            onLog?.Invoke($"Plików MP3: {res.Total}");
            onLog?.Invoke($"Zaktualizowano: {res.Updated}");
            onLog?.Invoke($"Bez zmian: {res.Unchanged}");
            onLog?.Invoke($"Brak w bazie: {res.Missing}");
            onLog?.Invoke($"Błędy: {res.Errors}");

            if (csv != null)
            {
                res.CsvPath = csv.Path;
                csv.Dispose();
                onLog?.Invoke($"Raport CSV: {csv.Path}");
            }

            if (backup != null)
            {
                backup.Dispose();
                onLog?.Invoke($"Backup sesji: {backup.Path}");
            }

            return res;
        }

        private static Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)> LoadDatabase(string jsonPath)
        {
            var text = IOFile.ReadAllText(jsonPath);
            var arr = JArray.Parse(text);
            var map = new Dictionary<(string, string, string), (List<string>, List<string>)>();

            foreach (var it in arr)
            {
                string artist = TaggingText.Norm((string?)it["artist"]);
                string title  = TaggingText.Norm((string?)it["title"]);
                string version= TaggingText.Norm((string?)it["version"]);
                string genres = (string?)it["genres"] ?? "";
                string labels = (string?)it["labels"] ?? "";

                var gList = TaggingText.CleanGenreList(genres);
                var lList = TaggingText.CleanLabelList(labels);

                map[(artist, title, version)] = (gList, lList);
                if (!string.IsNullOrEmpty(version))
                    map.TryAdd((artist, title, ""), (gList, lList));
            }
            return map;
        }

        private static ChangeRecord ProcessOneWithRetry(
            string path,
            Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)> db,
            TaggingOptions flags,
            Action<string>? onLog,
            Func<string, TagLib.File> fileFactory,
            CancellationToken cancellationToken,
            int maxAttempts = 3)
        {
            for (int attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return ProcessOne(path, db, flags, onLog, fileFactory);
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    onLog?.Invoke($"WARN: {Path.GetFileName(path)} -> błąd I/O, ponawiam ({attempt}/{maxAttempts - 1}): {ex.Message}");
                    WaitBeforeRetry(attempt, cancellationToken);
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxAttempts)
                {
                    onLog?.Invoke($"WARN: {Path.GetFileName(path)} -> brak dostępu, ponawiam ({attempt}/{maxAttempts - 1}): {ex.Message}");
                    WaitBeforeRetry(attempt, cancellationToken);
                }
            }
        }

        private static void WaitBeforeRetry(int attempt, CancellationToken cancellationToken)
        {
            var delayMs = 150 * attempt;
            if (cancellationToken.WaitHandle.WaitOne(delayMs))
                cancellationToken.ThrowIfCancellationRequested();
        }

        private static ChangeRecord ProcessOne(
            string path,
            Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)> db,
            TaggingOptions flags,
            Action<string>? onLog,
            Func<string, TagLib.File> fileFactory)
        {
            using var file = fileFactory(path);
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, true);

            var (artist, title, version) = TaggingText.ExtractArtistTitleVersion(file);

            var beforeGenres = string.Join(TaggingText.Sep, (file.Tag.Genres ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
            var beforeLabel = file.Tag.Publisher ?? "";

            if (!TaggingResolver.TryResolveInfo(path, artist, title, version, db, flags.FilenameFallback, out var info))
            {
                onLog?.Invoke($"MISSING: {Path.GetFileName(path)} -> [{artist} | {title} | {version}]");
                return TaggingApplier.CreateRecord(ChangeKind.Missing, beforeGenres, beforeGenres, beforeLabel, beforeLabel);
            }

            var genreChanged = TaggingApplier.ApplyGenreUpdate(file, flags, info.genres, beforeGenres, out var afterGenres);
            var labelChanged = TaggingApplier.ApplyLabelUpdate(file, id3v2, flags, info.labels, beforeLabel, out var afterLabel);
            var changed = genreChanged || labelChanged;

            if (changed)
            {
                if (!flags.DryRun)
                    file.Save();

                onLog?.Invoke($"UPDATED: {Path.GetFileName(path)}");
                return TaggingApplier.CreateRecord(ChangeKind.Updated, beforeGenres, afterGenres, beforeLabel, afterLabel);
            }

            onLog?.Invoke($"OK: {Path.GetFileName(path)}");
            return TaggingApplier.CreateRecord(ChangeKind.Unchanged, beforeGenres, afterGenres, beforeLabel, afterLabel);
        }

    }
}
