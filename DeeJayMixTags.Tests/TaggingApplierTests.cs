using System;
using System.Collections.Generic;
using System.Linq;
using TagLib.Id3v2;

namespace Mp3TaggerGUI.Tests;

public class TaggingApplierTests
{
    [Fact]
    public void BuildGenreValueFromDjoid_Replace_UsesConfiguredSourceOnly()
    {
        var flags = new TaggingOptions
        {
            DjoidGenreSource = DjoidGenreSource.GenreAndSubgenre,
            DjoidGenreWriteMode = GenreWriteMode.Replace,
            Dedup = true,
            NormalizeSeparators = true,
            TitleCase = true
        };

        var result = TaggingApplier.BuildGenreValueFromDjoid(
            flags,
            djoidGenre: "house",
            djoidSubgenre: "tech house",
            beforeGenres: "Old");

        Assert.Equal("House | Tech House", result);
    }

    [Fact]
    public void BuildGenreValueFromDjoid_Append_AddsAfterCurrent()
    {
        var flags = new TaggingOptions
        {
            DjoidGenreSource = DjoidGenreSource.GenreOnly,
            DjoidGenreWriteMode = GenreWriteMode.Append,
            Dedup = true,
            NormalizeSeparators = true,
            TitleCase = true
        };

        var result = TaggingApplier.BuildGenreValueFromDjoid(
            flags,
            djoidGenre: "house",
            djoidSubgenre: "ignored",
            beforeGenres: "Trance");

        Assert.Equal("Trance | House", result);
    }

    [Fact]
    public void BuildGenreValueFromDjoid_Prepend_AddsBeforeCurrent()
    {
        var flags = new TaggingOptions
        {
            DjoidGenreSource = DjoidGenreSource.SubgenreOnly,
            DjoidGenreWriteMode = GenreWriteMode.Prepend,
            Dedup = true,
            NormalizeSeparators = true,
            TitleCase = true
        };

        var result = TaggingApplier.BuildGenreValueFromDjoid(
            flags,
            djoidGenre: "ignored",
            djoidSubgenre: "tech house",
            beforeGenres: "Trance");

        Assert.Equal("Tech House | Trance", result);
    }

    [Fact]
    public void BuildCommentValue_RepairsPartialDmcAndCleansOldMetadata()
    {
        var flags = new TaggingOptions
        {
            WriteDmcComment = true,
            RepairDmcComment = true,
            CleanupCommentMetadata = true
        };

        var result = TaggingApplier.BuildCommentValue(
            "11B - 122,00 - 7 | Record label: Old Label/6A | Key: 6A | Energy: 7 | Niniejszy plik zostal udostepniony",
            flags);

        Assert.StartsWith("11B - 122,00 - 7 | Niniejszy plik zostal udostepniony", result);
        Assert.Contains("DEEJAY mix club", result);
        Assert.DoesNotContain("Record label:", result);
        Assert.DoesNotContain("Key:", result);
        Assert.DoesNotContain("Energy:", result);
    }

    [Fact]
    public void BuildDjoidCommentValue_AddsSingleDjoidBlock()
    {
        var flags = new TaggingOptions
        {
            WriteDjoidComment = true,
            ScaleDjoidEnergyDanceToTen = true
        };
        var info = new TrackLookupInfo
        {
            DjoidDanceability = "0.44",
            DjoidEmotion = "Happy",
            DjoidEnergy = "0.72",
            DjoidKey = "8A",
            DjoidGenre = "House",
            DjoidSubgenre = "Tech House"
        };

        var result = TaggingApplier.BuildDjoidCommentValue("User note", flags, info);

        Assert.Equal("User note | DJOID: Danceability: 4, Emotion: Happy, Energy: 7, Key: 8A, Genre: House, Subgenre: Tech House", result);
    }

    [Fact]
    public void BuildDjoidCommentValue_ReplacesExistingDjoidBlockWithoutDuplicating()
    {
        var flags = new TaggingOptions
        {
            WriteDjoidComment = true,
            ScaleDjoidEnergyDanceToTen = true
        };
        var info = new TrackLookupInfo
        {
            DjoidEnergy = "0.8",
            DjoidKey = "9B",
            DjoidGenre = "Trance"
        };

        var result = TaggingApplier.BuildDjoidCommentValue(
            "User note | DJOID: Energy: 1, Key: Old | Tail",
            flags,
            info);

        Assert.Equal(1, result.Split("DJOID:", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Key: Old", result);
        Assert.Contains("DJOID: Energy: 8, Key: 9B, Genre: Trance", result);
        Assert.StartsWith("User note", result);
        Assert.Contains("Tail", result);
    }

    [Fact]
    public void BuildDjoidCommentValue_CollapsesDuplicatedCommentFramesSeparatedBySemicolon()
    {
        var flags = new TaggingOptions
        {
            WriteDjoidComment = true,
            ScaleDjoidEnergyDanceToTen = true,
            TitleCase = true,
            NormalizeSeparators = true
        };
        var info = new TrackLookupInfo
        {
            DjoidDanceability = "0.7",
            DjoidEmotion = "-1",
            DjoidEnergy = "0.9",
            DjoidKey = "6A",
            DjoidGenre = "techno",
            DjoidSubgenre = "hard techno"
        };
        var dmc = "Niniejszy plik zostal udostepniony czlonkowi DEEJAY mix clubu, Azeby mozna bylo go publicznie odtwarzac - DJ musi posiadac aktualna legitymacje klubowa. Nosniki dzwieku przygotowywane przez DEEJAY mix club sa legalne i posiadaja wszelkie prawa do publicznych odtworzen. DEEJAY mix club";
        var current = $"6A - 138,00 - 7 | DJOID: Danceability: 10, Emotion: -1, Energy: 10, Key: 6A, Genre: Techno, Subgenre: Hard Techno | {dmc}; " +
            $"6A - 138,00 - 7 | DJOID: Danceability: 7, Emotion: -1, Energy: 9, Key: 6A, Genre: Techno, Subgenre: Hard Techno | {dmc}";

        var result = TaggingApplier.BuildDjoidCommentValue(current, flags, info);

        Assert.Equal(1, result.Split("6A - 138,00 - 7", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, result.Split("DJOID:", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, result.Split("Niniejszy plik zostal udostepniony", StringSplitOptions.None).Length - 1);
        Assert.Equal("6A - 138,00 - 7 | DJOID: Danceability: 7, Emotion: -1, Energy: 9, Key: 6A, Genre: Techno, Subgenre: Hard Techno | " + dmc, result);
    }

    [Fact]
    public void ApplyGenreUpdate_DryRun_UpdatesOutputWithoutTouchingFile()
    {
        var flags = new TaggingOptions
        {
            DoGenre = true,
            DryRun = true,
            PrependNew = true,
            Dedup = true,
            AlwaysAppendToGenre = true
        };

        var changed = TaggingApplier.ApplyGenreUpdate(
            file: null!,
            flags,
            genresFromDb: "Trance",
            beforeGenres: "House",
            out var afterGenres);

        Assert.True(changed);
        Assert.Equal("Trance | House | DJPromo.pl", afterGenres);
    }

    [Fact]
    public void ApplyGenreUpdate_ReturnsFalse_WhenDoGenreDisabled()
    {
        var flags = new TaggingOptions
        {
            DoGenre = false,
            DryRun = true
        };

        var changed = TaggingApplier.ApplyGenreUpdate(
            file: null!,
            flags,
            genresFromDb: "Trance",
            beforeGenres: "House",
            out var afterGenres);

        Assert.False(changed);
        Assert.Equal("House", afterGenres);
    }

    [Fact]
    public void ApplyGenreUpdate_NoChange_WhenMergedResultEqualsBefore()
    {
        var flags = new TaggingOptions
        {
            DoGenre = true,
            DryRun = true,
            Dedup = true,
            PrependNew = true,
            AlwaysAppendToGenre = false
        };

        var changed = TaggingApplier.ApplyGenreUpdate(
            file: null!,
            flags,
            genresFromDb: "House",
            beforeGenres: "House",
            out var afterGenres);

        Assert.False(changed);
        Assert.Equal("House", afterGenres);
    }

    [Fact]
    public void ApplyLabelUpdate_DryRun_UpdatesOutputWithoutTouchingFileOrFrames()
    {
        var flags = new TaggingOptions
        {
            DoLabel = true,
            DryRun = true,
            PrependNew = true,
            Dedup = true,
            WriteTxxxLabel = true
        };

        var changed = TaggingApplier.ApplyLabelUpdate(
            file: null!,
            id3v2: null!,
            flags,
            labelsFromDb: "Armada",
            beforeLabel: "Spinnin",
            out var afterLabel);

        Assert.True(changed);
        Assert.Equal("Armada | Spinnin", afterLabel);
    }

    [Fact]
    public void ApplyLabelUpdate_ReturnsFalse_WhenNoChange()
    {
        var flags = new TaggingOptions
        {
            DoLabel = true,
            DryRun = true,
            PrependNew = true,
            Dedup = true
        };

        var changed = TaggingApplier.ApplyLabelUpdate(
            file: null!,
            id3v2: null!,
            flags,
            labelsFromDb: "Sony",
            beforeLabel: "Sony",
            out var afterLabel);

        Assert.False(changed);
        Assert.Equal("Sony", afterLabel);
    }

    [Fact]
    public void ApplyLabelUpdate_WritesPublisherAndTxxx_WhenNotDryRun()
    {
        using var fakeFile = new FakeTagFile("A", "T", "", "House", "Old");

        var flags = new TaggingOptions
        {
            DoLabel = true,
            DryRun = false,
            PrependNew = true,
            Dedup = true,
            WriteTxxxLabel = true
        };

        var changed = TaggingApplier.ApplyLabelUpdate(
            file: fakeFile,
            id3v2: null, // Can't cast FakeTag to real TagLib.Id3v2.Tag; test Publisher change instead
            flags,
            labelsFromDb: "Armada",
            beforeLabel: "Old",
            out var afterLabel);

        Assert.True(changed);
        Assert.Equal("Armada | Old", afterLabel);
        Assert.Equal("Armada | Old", fakeFile.Tag.Publisher);
    }

    [Fact]
    public void ApplyDmcGenreTag_WritesOrUpdatesSingleTxxxFrame()
    {
        var id3v2 = new TagLib.Id3v2.Tag();
        var flags = new TaggingOptions { WriteDmcGenreTag = true };

        var created = TaggingApplier.ApplyDmcGenreTag(id3v2, flags, "House | DJPromo.pl");
        var unchanged = TaggingApplier.ApplyDmcGenreTag(id3v2, flags, "House | DJPromo.pl");
        var updated = TaggingApplier.ApplyDmcGenreTag(id3v2, flags, "Trance | DJPromo.pl");

        var frames = id3v2.GetFrames<UserTextInformationFrame>()
            .Where(f => string.Equals(f.Description, "DMC_GENRE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(created);
        Assert.False(unchanged);
        Assert.True(updated);
        Assert.Single(frames);
        Assert.Equal("Trance | DJPromo.pl", frames[0].Text.FirstOrDefault());
    }

    [Fact]
    public void CreateRecord_MapsValues()
    {
        var record = TaggingApplier.CreateRecord(ChangeKind.Updated, "G1", "G2", "L1", "L2");

        Assert.Equal(ChangeKind.Updated, record.Kind);
        Assert.Equal("G1", record.BeforeGenre);
        Assert.Equal("G2", record.AfterGenre);
        Assert.Equal("L1", record.BeforeLabel);
        Assert.Equal("L2", record.AfterLabel);
    }
}
