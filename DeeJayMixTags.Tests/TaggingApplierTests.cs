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
