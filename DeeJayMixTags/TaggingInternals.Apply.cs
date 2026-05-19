using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;

namespace Mp3TaggerGUI
{
    internal static class TaggingApplier
    {
        private const string DmcComment = "Niniejszy plik zostal udostepniony czlonkowi DEEJAY mix clubu, Azeby mozna bylo go publicznie odtwarzac - DJ musi posiadac aktualna legitymacje klubowa. Nosniki dzwieku przygotowywane przez DEEJAY mix club sa legalne i posiadaja wszelkie prawa do publicznych odtworzen. DEEJAY mix club";
        private static readonly Regex DmcFullRegex = new(
            @"(?is)(\s*(\|\s*|-\s*))?Niniejszy plik zostal udostepniony.*?publicznych odtworzen\.\s*DEEJAY mix club\b\.?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DmcFallbackRegex = new(
            @"(?is)(\s*(\|\s*|-\s*))?Niniejszy plik zostal udostepniony.*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CommentMetadataRegex = new(
            @"(?is)\s*(\|\s*|-\s*)Record\s*label\s*:\s*[^|]+"
            + @"|\s*(\|\s*|-\s*)Key\s*:?\s*\d{1,2}[AB]\b"
            + @"|\s*(\|\s*|-\s*)Energy\s*:?\s*\d+\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DjoidCommentRegex = new(
            @"(?is)(\s*(\|\s*|-\s*))?DJOID\s*:\s*[^|]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool ApplyGenreUpdate(
            TagLib.File file,
            TaggingOptions flags,
            string genresFromDb,
            string beforeGenres,
            out string afterGenres)
        {
            afterGenres = beforeGenres;
            if (!flags.DoGenre) return false;

            afterGenres = BuildGenreValue(flags, genresFromDb, beforeGenres);

            if (string.Equals(beforeGenres, afterGenres, StringComparison.Ordinal))
                return false;

            if (!flags.DryRun)
                file.Tag.Genres = [afterGenres];

            return true;
        }

        public static bool ApplyLabelUpdate(
            TagLib.File file,
            TagLib.Id3v2.Tag? id3v2,
            TaggingOptions flags,
            string labelsFromDb,
            string beforeLabel,
            out string afterLabel)
        {
            afterLabel = beforeLabel;
            if (!flags.DoLabel) return false;

            afterLabel = BuildLabelValue(flags, labelsFromDb, beforeLabel);

            bool publisherChanged = !string.Equals(beforeLabel, afterLabel, StringComparison.Ordinal);
            bool txxxChanged = false;

            if (flags.WriteTxxxLabel && id3v2 != null)
            {
                txxxChanged = SetOrUpdateTxxx(id3v2, "LABEL", afterLabel, flags.DryRun);
            }

            if (publisherChanged)
            {
                if (!flags.DryRun)
                {
                    file.Tag.Publisher = afterLabel;
                }
                return true;
            }

            // Return true if TXXX was changed even if Publisher wasn't
            return txxxChanged;
        }

        public static string BuildGenreValue(TaggingOptions flags, string genresFromDb, string beforeGenres)
        {
            var oldList = TaggingText.CleanGenreList(beforeGenres, flags);
            var newFromDb = TaggingText.CleanGenreList(genresFromDb, flags);

            var merged = TaggingText.MergeValues(newFromDb, oldList, flags.PrependNew, flags.Dedup);

            if (flags.AlwaysAppendToGenre && !merged.Any(x => x.Equals(TaggingText.AlwaysAppendToken, StringComparison.OrdinalIgnoreCase)))
                merged.Add(TaggingText.AlwaysAppendToken);

            return TaggingText.Join(merged);
        }

        public static string BuildGenreValueFromDjoid(TaggingOptions flags, string djoidGenre, string djoidSubgenre, string beforeGenres)
        {
            var source = new List<string>();
            if (flags.DjoidGenreSource == DjoidGenreSource.GenreOnly || flags.DjoidGenreSource == DjoidGenreSource.GenreAndSubgenre)
                source.AddRange(TaggingText.CleanGenreList(djoidGenre, flags));
            if (flags.DjoidGenreSource == DjoidGenreSource.SubgenreOnly || flags.DjoidGenreSource == DjoidGenreSource.GenreAndSubgenre)
                source.AddRange(TaggingText.CleanGenreList(djoidSubgenre, flags));

            source = TaggingText.Dedup(source);
            if (source.Count == 0)
                return beforeGenres;

            if (flags.DjoidGenreWriteMode == GenreWriteMode.Replace)
                return TaggingText.Join(source);

            var current = TaggingText.CleanGenreList(beforeGenres, flags);
            var merged = flags.DjoidGenreWriteMode == GenreWriteMode.Prepend
                ? source.Concat(current).ToList()
                : current.Concat(source).ToList();

            if (flags.Dedup)
                merged = TaggingText.Dedup(merged);

            return TaggingText.Join(merged);
        }

        public static string BuildLabelValue(TaggingOptions flags, string labelsFromDb, string beforeLabel)
        {
            var oldLabels = TaggingText.CleanLabelList(beforeLabel, flags);
            var newFromDb = TaggingText.CleanLabelList(labelsFromDb, flags);

            var merged = TaggingText.MergeValues(newFromDb, oldLabels, flags.PrependNew, flags.Dedup);
            return TaggingText.Join(merged);
        }

        public static bool ApplyManualUpdate(
            TagLib.File file,
            TagLib.Id3v2.Tag? id3v2,
            TaggingOptions flags,
            string targetGenre,
            string targetLabel,
            string beforeGenres,
            string beforeLabel,
            out string afterGenres,
            out string afterLabel)
        {
            afterGenres = beforeGenres;
            afterLabel = beforeLabel;
            var changed = false;

            if (flags.DoGenre)
            {
                afterGenres = TaggingText.Join(TaggingText.CleanGenreList(targetGenre, flags));
                if (!string.Equals(beforeGenres, afterGenres, StringComparison.Ordinal))
                {
                    if (!flags.DryRun)
                        file.Tag.Genres = string.IsNullOrWhiteSpace(afterGenres) ? [] : [afterGenres];
                    changed = true;
                }
            }

            if (flags.DoLabel)
            {
                afterLabel = TaggingText.Join(TaggingText.CleanLabelList(targetLabel, flags));
                if (!string.Equals(beforeLabel, afterLabel, StringComparison.Ordinal))
                {
                    if (!flags.DryRun)
                        file.Tag.Publisher = afterLabel;
                    changed = true;
                }

                if (flags.WriteTxxxLabel && id3v2 != null)
                    changed = SetOrUpdateTxxx(id3v2, "LABEL", afterLabel, flags.DryRun) || changed;
            }

            return changed;
        }

        public static bool ApplyCommentOptions(
            TagLib.File file,
            TaggingOptions flags,
            out string afterComment)
        {
            var beforeComment = file.Tag.Comment ?? "";
            afterComment = BuildCommentValue(beforeComment, flags);
            if (string.Equals(beforeComment, afterComment, StringComparison.Ordinal))
                return false;

            if (!flags.DryRun)
                file.Tag.Comment = afterComment;

            return true;
        }

        public static string BuildCommentValue(string currentComment, TaggingOptions flags)
        {
            var current = currentComment ?? "";
            if (flags.CleanupCommentMetadata)
                current = CommentMetadataRegex.Replace(current, "");

            var hasFullDmc = current.IndexOf(DmcComment, StringComparison.OrdinalIgnoreCase) >= 0;
            var hasAnyDmc = current.IndexOf("Niniejszy plik zostal udostepniony", StringComparison.OrdinalIgnoreCase) >= 0;

            if (flags.RepairDmcComment || (flags.WriteDmcComment && hasAnyDmc && !hasFullDmc))
            {
                current = DmcFullRegex.Replace(current, "");
                current = DmcFallbackRegex.Replace(current, "");
                hasFullDmc = false;
            }

            current = NormalizeCommentSeparators(current);

            if (flags.WriteDmcComment && !hasFullDmc)
                current = string.IsNullOrWhiteSpace(current) ? DmcComment : $"{current}{TaggingText.Sep}{DmcComment}";

            return NormalizeCommentSeparators(current);
        }

        public static bool ApplyDjoidComment(
            TagLib.File file,
            TaggingOptions flags,
            TrackLookupInfo info,
            out string afterComment)
        {
            var beforeComment = file.Tag.Comment ?? "";
            afterComment = BuildDjoidCommentValue(beforeComment, flags, info);
            if (string.Equals(beforeComment, afterComment, StringComparison.Ordinal))
                return false;

            if (!flags.DryRun)
                file.Tag.Comment = afterComment;

            return true;
        }

        public static string BuildDjoidCommentValue(string currentComment, TaggingOptions flags, TrackLookupInfo info)
        {
            var current = currentComment ?? "";
            var dmcMatch = DmcFullRegex.Match(current);
            var dmcComment = dmcMatch.Success ? DmcComment : "";

            current = DmcFullRegex.Replace(current, "");
            current = DmcFallbackRegex.Replace(current, "");
            current = DjoidCommentRegex.Replace(current, "");

            var parts = new List<string>();
            AddCommentPart(parts, "Danceability", ScaleDjoidNumeric(info.DjoidDanceability, flags.ScaleDjoidEnergyDanceToTen));
            AddCommentPart(parts, "Emotion", info.DjoidEmotion);
            AddCommentPart(parts, "Energy", ScaleDjoidNumeric(info.DjoidEnergy, flags.ScaleDjoidEnergyDanceToTen));
            AddCommentPart(parts, "Key", info.DjoidKey);
            AddCommentPart(parts, "Genre", CleanDjoidGenreForTag(info.DjoidGenre, flags));
            AddCommentPart(parts, "Subgenre", CleanDjoidGenreForTag(info.DjoidSubgenre, flags));

            var normalized = NormalizeCommentSeparators(current);
            if (parts.Count > 0)
            {
                var djoidBlock = "DJOID: " + string.Join(", ", parts);
                normalized = string.IsNullOrWhiteSpace(normalized) ? djoidBlock : $"{normalized}{TaggingText.Sep}{djoidBlock}";
            }

            if (!string.IsNullOrWhiteSpace(dmcComment))
                normalized = string.IsNullOrWhiteSpace(normalized) ? dmcComment : $"{normalized}{TaggingText.Sep}{dmcComment}";

            return NormalizeCommentSeparators(normalized);
        }

        public static bool ApplyDjoidTags(
            TagLib.Id3v2.Tag? id3v2,
            TaggingOptions flags,
            TrackLookupInfo info)
        {
            if (id3v2 == null)
                return false;

            var changed = false;
            if (flags.WriteDjoidGenreTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_GENRE", CleanDjoidGenreForTag(info.DjoidGenre, flags), flags.DryRun) || changed;
            if (flags.WriteDjoidSubgenreTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_SUBGENRE", CleanDjoidGenreForTag(info.DjoidSubgenre, flags), flags.DryRun) || changed;
            if (flags.WriteDjoidEnergyTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_ENERGY", ScaleDjoidNumeric(info.DjoidEnergy, flags.ScaleDjoidEnergyDanceToTen), flags.DryRun) || changed;
            if (flags.WriteDjoidDanceabilityTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_DANCEABILITY", ScaleDjoidNumeric(info.DjoidDanceability, flags.ScaleDjoidEnergyDanceToTen), flags.DryRun) || changed;
            if (flags.WriteDjoidEmotionTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_EMOTION", info.DjoidEmotion, flags.DryRun) || changed;
            if (flags.WriteDjoidKeyTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_KEY", info.DjoidKey, flags.DryRun) || changed;
            if (flags.WriteDjoidBpmTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_BPM", info.DjoidBpm, flags.DryRun) || changed;

            return changed;
        }

        public static bool ApplyDmcGenreTag(TagLib.Id3v2.Tag? id3v2, TaggingOptions flags, string dmcGenre)
        {
            if (!flags.WriteDmcGenreTag || id3v2 == null || string.IsNullOrWhiteSpace(dmcGenre))
                return false;

            return SetOrUpdateTxxx(id3v2, "DMC_GENRE", dmcGenre, flags.DryRun);
        }

        private static string ScaleDjoidNumeric(string raw, bool scaleToTen)
        {
            if (!scaleToTen || string.IsNullOrWhiteSpace(raw))
                return raw ?? "";
            if (!double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                return raw;
            if (d <= 1.0)
                d *= 10.0;
            d = Math.Clamp(d, 0.0, 10.0);
            return Math.Round(d).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string SnapshotTxxx(TagLib.Id3v2.Tag? id3)
        {
            if (id3 == null)
                return "";

            return string.Join(TaggingText.Sep,
                id3.GetFrames<UserTextInformationFrame>()
                    .Where(f => !string.IsNullOrWhiteSpace(f.Description))
                    .OrderBy(f => f.Description, StringComparer.OrdinalIgnoreCase)
                    .Select(f => $"{f.Description}={f.Text.FirstOrDefault() ?? ""}"));
        }

        private static bool SetOrUpdateTxxx(TagLib.Id3v2.Tag id3, string desc, string? value, bool dryRun)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var txxx = id3.GetFrames<UserTextInformationFrame>().FirstOrDefault(
                f => string.Equals(f.Description, desc, StringComparison.OrdinalIgnoreCase));
            if (txxx == null)
            {
                if (dryRun)
                    return true;

                txxx = new UserTextInformationFrame(desc) { TextEncoding = StringType.UTF16 };
                id3.AddFrame(txxx);
            }
            if (!string.Equals((txxx.Text.FirstOrDefault() ?? ""), value, StringComparison.Ordinal))
            {
                if (!dryRun)
                    txxx.Text = [value];
                return true;
            }
            return false;
        }

        private static void AddCommentPart(List<string> parts, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            parts.Add($"{label}: {value.Trim()}");
        }

        private static string CleanDjoidGenreForTag(string value, TaggingOptions flags)
        {
            return TaggingText.Join(TaggingText.CleanGenreList(value, flags));
        }

        public static ChangeRecord CreateRecord(
            ChangeKind kind,
            string beforeGenre,
            string afterGenre,
            string beforeLabel,
            string afterLabel,
            string beforeComment = "",
            string afterComment = "",
            string beforeTxxx = "",
            string afterTxxx = "",
            string changedFields = "") => new()
            {
                Kind = kind,
                BeforeGenre = beforeGenre,
                AfterGenre = afterGenre,
                BeforeLabel = beforeLabel,
                AfterLabel = afterLabel,
                BeforeComment = beforeComment,
                AfterComment = afterComment,
                BeforeTxxx = beforeTxxx,
                AfterTxxx = afterTxxx,
                ChangedFields = changedFields
            };

        private static string NormalizeCommentSeparators(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var s = value.Trim();
            s = Regex.Replace(s, @"\s*\|\s*", TaggingText.Sep);
            s = Regex.Replace(s, @"(\s*\|\s*){2,}", TaggingText.Sep);
            s = Regex.Replace(s, @"^(\s*\|\s*|\s*-\s*)+", "");
            s = Regex.Replace(s, @"(\s*\|\s*|\s*-\s*)+$", "");
            s = Regex.Replace(s, @"\s{2,}", " ");
            return s.Trim();
        }
    }
}
