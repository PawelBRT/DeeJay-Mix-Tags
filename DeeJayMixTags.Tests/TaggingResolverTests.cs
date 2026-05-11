using System.Collections.Generic;

namespace Mp3TaggerGUI.Tests;

public class TaggingResolverTests
{
    [Fact]
    public void TryResolveInfo_FindsExactMatch()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>
        {
            [("Artist", "Title", "Club Mix")] = (new List<string> { "POP" }, new List<string> { "Sony" })
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Club Mix", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal(new List<string> { "POP" }, info.genres);
        Assert.Equal(new List<string> { "Sony" }, info.labels);
    }

    [Fact]
    public void TryResolveInfo_UsesVersionlessFallback()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>
        {
            [("Artist", "Title", "")] = (new List<string> { "House" }, new List<string> { "Armada" })
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Any Version", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal(new List<string> { "House" }, info.genres);
        Assert.Equal(new List<string> { "Armada" }, info.labels);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WhenEnabled()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>
        {
            [("Alice", "Track", "")] = (new List<string> { "Trance" }, new List<string> { "Label" })
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/Alice - Track_x1.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal(new List<string> { "Trance" }, info.genres);
        Assert.Equal(new List<string> { "Label" }, info.labels);
    }

    [Fact]
    public void TryResolveInfo_ReturnsFalse_WhenNoMatchAndFallbackDisabled()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>();

        var ok = TaggingResolver.TryResolveInfo("C:/music/Unknown - Song.mp3", "Unknown", "Song", "", db, allowFilenameFallback: false, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>
        {
            [("A", "T", "Club Mix")] = (new List<string> { "POP" }, new List<string> { "Sony" })
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Club Mix)_abc123.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal(new List<string> { "POP" }, info.genres);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionlessFallbackFromVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), (List<string> genres, List<string> labels)>
        {
            [("A", "T", "")] = (new List<string> { "House" }, new List<string> { "Armada" })
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Unknown Mix).mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal(new List<string> { "House" }, info.genres);
    }
}
