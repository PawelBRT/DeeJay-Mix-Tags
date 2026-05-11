using System.Collections.Generic;

namespace Mp3TaggerGUI.Tests;

public class TaggingResolverTests
{
    [Fact]
    public void TryResolveInfo_FindsExactMatch()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>
        {
            [("Artist", "Title", "Club Mix")] = ("POP", "Sony")
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Club Mix", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal("POP", info.genres);
        Assert.Equal("Sony", info.labels);
    }

    [Fact]
    public void TryResolveInfo_UsesVersionlessFallback()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>
        {
            [("Artist", "Title", "")] = ("House", "Armada")
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Any Version", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal("House", info.genres);
        Assert.Equal("Armada", info.labels);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WhenEnabled()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>
        {
            [("Alice", "Track", "")] = ("Trance", "Label")
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/Alice - Track_x1.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("Trance", info.genres);
        Assert.Equal("Label", info.labels);
    }

    [Fact]
    public void TryResolveInfo_ReturnsFalse_WhenNoMatchAndFallbackDisabled()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>();

        var ok = TaggingResolver.TryResolveInfo("C:/music/Unknown - Song.mp3", "Unknown", "Song", "", db, allowFilenameFallback: false, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>
        {
            [("A", "T", "Club Mix")] = ("POP", "Sony")
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Club Mix)_abc123.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("POP", info.genres);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionlessFallbackFromVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), (string genres, string labels)>
        {
            [("A", "T", "")] = ("House", "Armada")
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Unknown Mix).mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("House", info.genres);
    }
}
