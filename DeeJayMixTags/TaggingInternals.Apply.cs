using System;
using System.Collections.Generic;
using System.Linq;
using TagLib;
using TagLib.Id3v2;

namespace Mp3TaggerGUI
{
    internal static class TaggingApplier
    {
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

            if (!flags.DryRun && flags.WriteTxxxLabel && id3v2 != null)
            {
                txxxChanged = EnsureTxxxLabel(id3v2, afterLabel);
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

                if (!flags.DryRun && flags.WriteTxxxLabel && id3v2 != null)
                    changed = EnsureTxxxLabel(id3v2, afterLabel) || changed;
            }

            return changed;
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
                changed = SetOrUpdateTxxx(id3v2, "DJOID_GENRE", info.DjoidGenre) || changed;
            if (flags.WriteDjoidSubgenreTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_SUBGENRE", info.DjoidSubgenre) || changed;
            if (flags.WriteDjoidEnergyTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_ENERGY", ScaleDjoidNumeric(info.DjoidEnergy, flags.ScaleDjoidEnergyDanceToTen)) || changed;
            if (flags.WriteDjoidDanceabilityTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_DANCEABILITY", ScaleDjoidNumeric(info.DjoidDanceability, flags.ScaleDjoidEnergyDanceToTen)) || changed;
            if (flags.WriteDjoidEmotionTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_EMOTION", info.DjoidEmotion) || changed;
            if (flags.WriteDjoidKeyTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_KEY", info.DjoidKey) || changed;
            if (flags.WriteDjoidBpmTag)
                changed = SetOrUpdateTxxx(id3v2, "DJOID_BPM", info.DjoidBpm) || changed;

            return changed;
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

        private static bool SetOrUpdateTxxx(TagLib.Id3v2.Tag id3, string desc, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var txxx = id3.GetFrames<UserTextInformationFrame>().FirstOrDefault(
                f => string.Equals(f.Description, desc, StringComparison.OrdinalIgnoreCase));
            var changed = false;
            if (txxx == null)
            {
                txxx = new UserTextInformationFrame(desc) { TextEncoding = StringType.UTF16 };
                id3.AddFrame(txxx);
                changed = true;
            }
            if (!string.Equals((txxx.Text.FirstOrDefault() ?? ""), value, StringComparison.Ordinal))
            {
                txxx.Text = [value];
                changed = true;
            }
            return changed;
        }

        public static ChangeRecord CreateRecord(
            ChangeKind kind,
            string beforeGenre,
            string afterGenre,
            string beforeLabel,
            string afterLabel) => new()
            {
                Kind = kind,
                BeforeGenre = beforeGenre,
                AfterGenre = afterGenre,
                BeforeLabel = beforeLabel,
                AfterLabel = afterLabel
            };

        private static bool EnsureTxxxLabel(TagLib.Id3v2.Tag id3, string value)
        {
            var txxx = id3.GetFrames<UserTextInformationFrame>().FirstOrDefault(
                f => string.Equals(f.Description, "LABEL", StringComparison.OrdinalIgnoreCase));

            bool changed = false;

            if (txxx == null)
            {
                txxx = new UserTextInformationFrame("LABEL") { TextEncoding = StringType.UTF16 };
                id3.AddFrame(txxx);
                changed = true;
            }

            if (!string.Equals((txxx.Text.FirstOrDefault() ?? ""), value, StringComparison.Ordinal))
            {
                txxx.Text = [value];
                changed = true;
            }

            return changed;
        }
    }
}
