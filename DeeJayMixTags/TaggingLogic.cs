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

        public static List<TagEditRow> LoadGridRows(
            string mp3Root,
            string jsonPath,
            TaggingOptions flags,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            var files = Directory.EnumerateFiles(mp3Root, "*.mp3", SearchOption.AllDirectories).ToList();
            return LoadGridRowsCore(jsonPath, flags, files, TagLib.File.Create, onTotal, onStep, onLog, cancellationToken);
        }

        internal static List<TagEditRow> LoadGridRowsCore(
            string jsonPath,
            TaggingOptions flags,
            IEnumerable<string> files,
            Func<string, TagLib.File>? fileFactory = null,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            var db = LoadDatabase(jsonPath, flags);
            var fileList = files.ToList();
            var openFile = fileFactory ?? TagLib.File.Create;
            var rows = new List<TagEditRow>();
            onTotal?.Invoke(fileList.Count);

            foreach (var path in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var file = openFile(path);
                    var (artist, title, version) = TaggingText.ExtractArtistTitleVersion(file);
                    var beforeGenres = string.Join(TaggingText.Sep, (file.Tag.Genres ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
                    var beforeLabel = file.Tag.Publisher ?? "";
                    var beforeAlbum = file.Tag.Album ?? "";
                    var beforeYear = file.Tag.Year == 0 ? "" : file.Tag.Year.ToString();
                    var beforeTrack = file.Tag.Track == 0 ? "" : file.Tag.Track.ToString();
                    var beforeBpm = file.Tag.BeatsPerMinute == 0 ? "" : file.Tag.BeatsPerMinute.ToString();
                    var beforeKey = file.Tag.InitialKey ?? "";
                    var beforeComment = file.Tag.Comment ?? "";
                    var targetGenre = beforeGenres;
                    var targetLabel = beforeLabel;
                    var status = "Brak w bazie";
                    var apply = false;

                    if (TaggingResolver.TryResolveInfo(path, artist, title, version, db, flags.FilenameFallback, out var info))
                    {
                        if (flags.DataSource == TagDataSource.DjoidJson)
                        {
                            targetGenre = flags.DjoidGenreSource == DjoidGenreSource.None
                                ? beforeGenres
                                : TaggingApplier.BuildGenreValueFromDjoid(flags, info.DjoidGenre, info.DjoidSubgenre, beforeGenres);
                            targetLabel = beforeLabel;
                        }
                        else
                        {
                            targetGenre = flags.DoGenre ? TaggingApplier.BuildGenreValue(flags, info.Genres, beforeGenres) : beforeGenres;
                            targetLabel = flags.DoLabel ? TaggingApplier.BuildLabelValue(flags, info.Labels, beforeLabel) : beforeLabel;
                        }
                        apply = !string.Equals(beforeGenres, targetGenre, StringComparison.Ordinal)
                            || !string.Equals(beforeLabel, targetLabel, StringComparison.Ordinal);
                        status = apply ? "Do zapisu" : "Bez zmian";
                    }

                    rows.Add(new TagEditRow
                    {
                        Apply = apply,
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        Artist = artist,
                        Title = title,
                        Version = version,
                        CurrentAlbum = beforeAlbum,
                        Album = beforeAlbum,
                        CurrentYear = beforeYear,
                        Year = beforeYear,
                        CurrentTrack = beforeTrack,
                        Track = beforeTrack,
                        CurrentBpm = beforeBpm,
                        Bpm = beforeBpm,
                        CurrentKey = beforeKey,
                        Key = beforeKey,
                        CurrentComment = beforeComment,
                        Comment = beforeComment,
                        CurrentGenre = beforeGenres,
                        CurrentLabel = beforeLabel,
                        Genre = targetGenre,
                        Label = targetLabel,
                        IsDjoid = flags.DataSource == TagDataSource.DjoidJson,
                        DjoidGenre = info.DjoidGenre,
                        DjoidSubgenre = info.DjoidSubgenre,
                        DjoidEnergy = info.DjoidEnergy,
                        DjoidDanceability = info.DjoidDanceability,
                        DjoidEmotion = info.DjoidEmotion,
                        DjoidKey = info.DjoidKey,
                        DjoidBpm = info.DjoidBpm,
                        Status = status
                    });
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"ERROR: {Path.GetFileName(path)} -> {ex.Message}");
                    rows.Add(new TagEditRow
                    {
                        Apply = false,
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        Status = $"Błąd: {ex.Message}"
                    });
                }
                finally { onStep?.Invoke(); }
            }

            return rows;
        }

        public static Result ApplyGridRows(
            string reportRoot,
            IEnumerable<TagEditRow> rows,
            TaggingOptions flags,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            return ApplyGridRowsCore(reportRoot, rows, flags, TagLib.File.Create, onTotal, onStep, onLog, cancellationToken);
        }

        internal static Result ApplyGridRowsCore(
            string reportRoot,
            IEnumerable<TagEditRow> rows,
            TaggingOptions flags,
            Func<string, TagLib.File>? fileFactory = null,
            Action<int>? onTotal = null,
            Action? onStep = null,
            Action<string>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            var rowList = rows.Where(r => r.Apply).ToList();
            var openFile = fileFactory ?? TagLib.File.Create;
            var res = new Result { Total = rowList.Count };
            onTotal?.Invoke(rowList.Count);

            TaggingCsvWriter? csv = null;
            TaggingBackupWriter? backup = null;

            try
            {
                csv = flags.WriteCsvReport ? new TaggingCsvWriter(Path.Combine(reportRoot, "_tagger_grid_report.csv")) : null;
                backup = (!flags.DryRun && flags.WritePerFileBackup)
                    ? new TaggingBackupWriter(Path.Combine(reportRoot, $"_tagger_grid_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"))
                    : null;

                foreach (var row in rowList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var file = openFile(row.FilePath);
                        var id3v2 = file.GetTag(TagTypes.Id3v2, true) as TagLib.Id3v2.Tag;
                        var beforeGenres = string.Join(TaggingText.Sep, (file.Tag.Genres ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
                        var beforeLabel = file.Tag.Publisher ?? "";
                        var changed = TaggingApplier.ApplyManualUpdate(
                            file, id3v2, flags, row.Genre, row.Label, beforeGenres, beforeLabel,
                            out var afterGenres, out var afterLabel);
                        if (!flags.DryRun)
                        {
                            var targetAlbum = row.Album?.Trim() ?? "";
                            if (!string.Equals((file.Tag.Album ?? ""), targetAlbum, StringComparison.Ordinal))
                            {
                                file.Tag.Album = targetAlbum;
                                changed = true;
                            }

                            var targetComment = row.Comment?.Trim() ?? "";
                            if (!string.Equals((file.Tag.Comment ?? ""), targetComment, StringComparison.Ordinal))
                            {
                                file.Tag.Comment = targetComment;
                                changed = true;
                            }

                            var targetKey = row.Key?.Trim() ?? "";
                            if (!string.Equals((file.Tag.InitialKey ?? ""), targetKey, StringComparison.Ordinal))
                            {
                                file.Tag.InitialKey = targetKey;
                                changed = true;
                            }

                            if (uint.TryParse((row.Year ?? "").Trim(), out var yearParsed))
                            {
                                if (file.Tag.Year != yearParsed)
                                {
                                    file.Tag.Year = yearParsed;
                                    changed = true;
                                }
                            }
                            else if (string.IsNullOrWhiteSpace(row.Year) && file.Tag.Year != 0)
                            {
                                file.Tag.Year = 0;
                                changed = true;
                            }

                            if (uint.TryParse((row.Track ?? "").Trim(), out var trackParsed))
                            {
                                if (file.Tag.Track != trackParsed)
                                {
                                    file.Tag.Track = trackParsed;
                                    changed = true;
                                }
                            }
                            else if (string.IsNullOrWhiteSpace(row.Track) && file.Tag.Track != 0)
                            {
                                file.Tag.Track = 0;
                                changed = true;
                            }

                            if (uint.TryParse((row.Bpm ?? "").Trim(), out var bpmParsed))
                            {
                                if (file.Tag.BeatsPerMinute != bpmParsed)
                                {
                                    file.Tag.BeatsPerMinute = bpmParsed;
                                    changed = true;
                                }
                            }
                            else if (string.IsNullOrWhiteSpace(row.Bpm) && file.Tag.BeatsPerMinute != 0)
                            {
                                file.Tag.BeatsPerMinute = 0;
                                changed = true;
                            }
                        }
                        if (!flags.DryRun && row.IsDjoid)
                        {
                            changed = TaggingApplier.ApplyDjoidTags(id3v2, flags, new TrackLookupInfo
                            {
                                DjoidGenre = row.DjoidGenre,
                                DjoidSubgenre = row.DjoidSubgenre,
                                DjoidEnergy = row.DjoidEnergy,
                                DjoidDanceability = row.DjoidDanceability,
                                DjoidEmotion = row.DjoidEmotion,
                                DjoidKey = row.DjoidKey,
                                DjoidBpm = row.DjoidBpm
                            }) || changed;
                        }

                        var kind = changed ? ChangeKind.Updated : ChangeKind.Unchanged;
                        if (changed)
                        {
                            if (!flags.DryRun)
                                file.Save();
                            res.Updated++;
                            row.Status = flags.DryRun ? "Dry run" : "Zapisano";
                            backup?.WriteRow(row.FilePath, beforeGenres, beforeLabel, afterGenres, afterLabel);
                        }
                        else
                        {
                            res.Unchanged++;
                            row.Status = "Bez zmian";
                        }

                        row.CurrentGenre = afterGenres;
                        row.CurrentLabel = afterLabel;
                        row.CurrentAlbum = file.Tag.Album ?? "";
                        row.CurrentYear = file.Tag.Year == 0 ? "" : file.Tag.Year.ToString();
                        row.CurrentTrack = file.Tag.Track == 0 ? "" : file.Tag.Track.ToString();
                        row.CurrentBpm = file.Tag.BeatsPerMinute == 0 ? "" : file.Tag.BeatsPerMinute.ToString();
                        row.CurrentKey = file.Tag.InitialKey ?? "";
                        row.CurrentComment = file.Tag.Comment ?? "";
                        csv?.WriteRow(TaggingApplier.CreateRecord(kind, beforeGenres, afterGenres, beforeLabel, afterLabel), row.FilePath);
                        onLog?.Invoke($"{row.Status}: {row.FileName}");
                    }
                    catch (Exception ex)
                    {
                        res.Errors++;
                        row.Status = $"Błąd: {ex.Message}";
                        onLog?.Invoke($"ERROR: {row.FileName} -> {ex.Message}");
                        csv?.WriteError(row.FilePath, ex.Message);
                    }
                    finally { onStep?.Invoke(); }
                }

                return res;
            }
            finally
            {
                if (csv != null)
                {
                    try
                    {
                        res.CsvPath = csv.Path;
                        csv.Dispose();
                        onLog?.Invoke($"Raport CSV: {csv.Path}");
                    }
                    catch { }
                }

                if (backup != null)
                {
                    try
                    {
                        backup.Dispose();
                        onLog?.Invoke($"Backup sesji: {backup.Path}");
                    }
                    catch { }
                }
            }
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
            var db = LoadDatabase(jsonPath, flags);
            var fileList = files.ToList();
            onTotal?.Invoke(fileList.Count);

            var openFile = fileFactory ?? TagLib.File.Create;
            var res = new Result { Total = fileList.Count };

            TaggingCsvWriter? csv = null;
            TaggingBackupWriter? backup = null;

            try
            {
                csv = flags.WriteCsvReport ? new TaggingCsvWriter(Path.Combine(reportRoot, "_tagger_report.csv")) : null;
                backup = (!flags.DryRun && flags.WritePerFileBackup)
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

                return res;
            }
            finally
            {
                if (csv != null)
                {
                    try
                    {
                        res.CsvPath = csv.Path;
                        csv.Dispose();
                        onLog?.Invoke($"Raport CSV: {csv.Path}");
                    }
                    catch { }
                }

                if (backup != null)
                {
                    try
                    {
                        backup.Dispose();
                        onLog?.Invoke($"Backup sesji: {backup.Path}");
                    }
                    catch { }
                }
            }
        }

        private static Dictionary<(string a, string t, string v), TrackLookupInfo> LoadDatabase(string jsonPath, TaggingOptions flags)
        {
            if (string.IsNullOrWhiteSpace(jsonPath) || !IOFile.Exists(jsonPath))
                return new();

            var text = IOFile.ReadAllText(jsonPath);
            var arr = JArray.Parse(text);
            var map = new Dictionary<(string, string, string), TrackLookupInfo>();

            if (flags.DataSource == TagDataSource.DjoidJson)
            {
                var djsEntry = arr.FirstOrDefault(x => string.Equals((string?)x["key"], "DJSAPP", StringComparison.OrdinalIgnoreCase));
                var tracksObj = djsEntry?["value"]?["tracks"]?["tracksByIds"] as JObject;
                if (tracksObj == null)
                    return map;

                foreach (var prop in tracksObj.Properties())
                {
                    var t = prop.Value as JObject;
                    if (t == null) continue;
                    var artistToken = t["artist"];
                    var artist = artistToken is JArray aArr
                        ? TaggingText.Norm(string.Join(" & ", aArr.Select(x => (string?)x).Where(x => !string.IsNullOrWhiteSpace(x))))
                        : TaggingText.Norm((string?)artistToken);
                    var title = TaggingText.Norm((string?)t["title"]);
                    var version = "";
                    var m = System.Text.RegularExpressions.Regex.Match(title, @"\(([^()]*)\)\s*$");
                    if (m.Success)
                    {
                        version = TaggingText.Norm(m.Groups[1].Value);
                        title = TaggingText.Norm(System.Text.RegularExpressions.Regex.Replace(title, @"\s*\([^()]*\)\s*$", ""));
                    }

                    var genres = t["genres"] is JArray gArr ? string.Join(" | ", gArr.Select(x => TaggingText.Norm((string?)x)).Where(x => x.Length > 0)) : TaggingText.Norm((string?)t["genres"]);
                    var subgenres = t["subgenres"] is JArray sgArr ? string.Join(" | ", sgArr.Select(x => TaggingText.Norm((string?)x)).Where(x => x.Length > 0)) : TaggingText.Norm((string?)t["subgenres"]);
                    string tokenText(string name) => t[name] == null ? "" : TaggingText.Norm(t[name]!.ToString());
                    var info = new TrackLookupInfo
                    {
                        DjoidGenre = genres ?? "",
                        DjoidSubgenre = subgenres ?? "",
                        DjoidEnergy = tokenText("energy"),
                        DjoidDanceability = tokenText("danceability"),
                        DjoidEmotion = tokenText("emotion"),
                        DjoidKey = tokenText("key"),
                        DjoidBpm = tokenText("bpm")
                    };

                    map[(artist, title, version)] = info;
                    if (!string.IsNullOrEmpty(version))
                        map.TryAdd((artist, title, ""), info);
                }

                return map;
            }

            foreach (var it in arr)
            {
                string artist = TaggingText.Norm((string?)it["artist"]);
                string title  = TaggingText.Norm((string?)it["title"]);
                string version= TaggingText.Norm((string?)it["version"]);
                string genres = (string?)it["genres"] ?? "";
                string labels = (string?)it["labels"] ?? "";

                map[(artist, title, version)] = new TrackLookupInfo { Genres = genres, Labels = labels };
                if (!string.IsNullOrEmpty(version))
                    map.TryAdd((artist, title, ""), new TrackLookupInfo { Genres = genres, Labels = labels });
            }
            return map;
        }

        private static ChangeRecord ProcessOneWithRetry(
            string path,
            Dictionary<(string a, string t, string v), TrackLookupInfo> db,
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
            Dictionary<(string a, string t, string v), TrackLookupInfo> db,
            TaggingOptions flags,
            Action<string>? onLog,
            Func<string, TagLib.File> fileFactory)
        {
            using var file = fileFactory(path);
            var id3v2 = file.GetTag(TagTypes.Id3v2, true) as TagLib.Id3v2.Tag;


            var (artist, title, version) = TaggingText.ExtractArtistTitleVersion(file);

            var beforeGenres = string.Join(TaggingText.Sep, (file.Tag.Genres ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
            var beforeLabel = file.Tag.Publisher ?? "";

            if (!TaggingResolver.TryResolveInfo(path, artist, title, version, db, flags.FilenameFallback, out var info))
            {
                onLog?.Invoke($"MISSING: {Path.GetFileName(path)} -> [{artist} | {title} | {version}]");
                return TaggingApplier.CreateRecord(ChangeKind.Missing, beforeGenres, beforeGenres, beforeLabel, beforeLabel);
            }

            var afterGenres = beforeGenres;
            var afterLabel = beforeLabel;
            var genreChanged = false;
            var labelChanged = false;

            if (flags.DataSource == TagDataSource.DjoidJson)
            {
                if (flags.DjoidGenreSource != DjoidGenreSource.None)
                {
                    afterGenres = TaggingApplier.BuildGenreValueFromDjoid(flags, info.DjoidGenre, info.DjoidSubgenre, beforeGenres);
                    genreChanged = !string.Equals(beforeGenres, afterGenres, StringComparison.Ordinal);
                    if (genreChanged && !flags.DryRun)
                        file.Tag.Genres = string.IsNullOrWhiteSpace(afterGenres) ? [] : [afterGenres];
                }

                if (!flags.DryRun)
                    labelChanged = TaggingApplier.ApplyDjoidTags(id3v2, flags, info);
            }
            else
            {
                genreChanged = TaggingApplier.ApplyGenreUpdate(file, flags, info.Genres, beforeGenres, out afterGenres);
                labelChanged = TaggingApplier.ApplyLabelUpdate(file, id3v2, flags, info.Labels, beforeLabel, out afterLabel);
            }

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
