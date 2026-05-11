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

            var oldList = TaggingText.CleanGenreList(beforeGenres, flags);
            var newFromDb = TaggingText.CleanGenreList(genresFromDb, flags);

            var merged = TaggingText.MergeValues(newFromDb, oldList, flags.PrependNew, flags.Dedup);

            if (flags.AlwaysAppendToGenre && !merged.Any(x => x.Equals(TaggingText.AlwaysAppendToken, StringComparison.OrdinalIgnoreCase)))
                merged.Add(TaggingText.AlwaysAppendToken);

            afterGenres = TaggingText.Join(merged);

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

            var oldLabels = TaggingText.CleanLabelList(beforeLabel, flags);
            var newFromDb = TaggingText.CleanLabelList(labelsFromDb, flags);

            var merged = TaggingText.MergeValues(newFromDb, oldLabels, flags.PrependNew, flags.Dedup);
            afterLabel = TaggingText.Join(merged);

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
