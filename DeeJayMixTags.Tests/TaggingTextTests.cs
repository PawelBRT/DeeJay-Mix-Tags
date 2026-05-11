using System.Collections.Generic;

namespace Mp3TaggerGUI.Tests;

public class TaggingTextTests
{
    [Fact]
    public void CleanGenreList_RemovesCountryPrefixAndDeduplicates()
    {
        var result = TaggingText.CleanGenreList("Polska: trance | Świat: POP | pop | Polska");

        Assert.Equal(new List<string> { "Trance", "POP" }, result);
    }

    [Fact]
    public void CleanLabelList_RemovesDot_TitleCases_AndDeduplicates()
    {
        var result = TaggingText.CleanLabelList("sony.; SONY; black hole records");

        Assert.Equal(new List<string> { "Sony", "Black Hole Records" }, result);
    }

    [Fact]
    public void MergeValues_WithPrependAndDedup_PreservesOrderOfFirstOccurrence()
    {
        var newer = new List<string> { "POP", "House" };
        var older = new List<string> { "House", "Trance" };

        var result = TaggingText.MergeValues(newer, older, prependNew: true, dedup: true);

        Assert.Equal(new List<string> { "POP", "House", "Trance" }, result);
    }

    [Fact]
    public void Norm_TrimAndCollapseWhitespace()
    {
        var result = TaggingText.Norm("  A   B\t C  ");

        Assert.Equal("A B C", result);
    }

    [Fact]
    public void SplitMulti_SplitsAllSupportedSeparators()
    {
        var result = TaggingText.SplitMulti("A/B;C:D,E|F");

        Assert.Equal(new List<string> { "A", "B", "C", "D", "E", "F" }, result);
    }

    [Fact]
    public void TitleToken_ForcePopUpper_UppercasesPopOnly()
    {
        Assert.Equal("POP", TaggingText.TitleToken("pop", forcePopUpper: true));
        Assert.Equal("Trance", TaggingText.TitleToken("TRANCE", forcePopUpper: true));
    }

    [Fact]
    public void Dedup_IsCaseInsensitive_AndSkipsEmptyValues()
    {
        var result = TaggingText.Dedup(new List<string> { "Sony", "", "  ", "sony", "Armada" });

        Assert.Equal(new List<string> { "Sony", "Armada" }, result);
    }

    [Fact]
    public void MergeValues_WithoutDedup_NormalizesAndKeepsDuplicates()
    {
        var result = TaggingText.MergeValues(
            newer: new List<string> { "  A  " },
            older: new List<string> { "A", "B" },
            prependNew: false,
            dedup: false);

        Assert.Equal(new List<string> { "A", "B", "A" }, result);
    }

    [Fact]
    public void Join_SkipsEmptyAndNormalizesValues()
    {
        var result = TaggingText.Join(new List<string> { " A ", "", "B" });

        Assert.Equal("A | B", result);
    }

    [Fact]
    public void ExtractArtistTitleVersion_ParsesVersionFromTitle()
    {
        using var file = new FakeTagFile("Artist", "My Song", "Extended Mix", "Genre", "Label");

        var (artist, title, version) = TaggingText.ExtractArtistTitleVersion(file);

        Assert.Equal("Artist", artist);
        Assert.Equal("My Song", title);
        Assert.Equal("Extended Mix", version);
    }
}
