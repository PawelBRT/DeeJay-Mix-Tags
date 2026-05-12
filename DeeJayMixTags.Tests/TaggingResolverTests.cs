using System.Collections.Generic;

namespace Mp3TaggerGUI.Tests;

public class TaggingResolverTests
{
    [Fact]
    public void TryResolveInfo_FindsExactMatch()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>
        {
            [("Artist", "Title", "Club Mix")] = new() { Genres = "POP", Labels = "Sony" }
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Club Mix", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal("POP", info.Genres);
        Assert.Equal("Sony", info.Labels);
    }

    [Fact]
    public void TryResolveInfo_UsesVersionlessFallback()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>
        {
            [("Artist", "Title", "")] = new() { Genres = "House", Labels = "Armada" }
        };

        var ok = TaggingResolver.TryResolveInfo("x.mp3", "Artist", "Title", "Any Version", db, allowFilenameFallback: false, out var info);

        Assert.True(ok);
        Assert.Equal("House", info.Genres);
        Assert.Equal("Armada", info.Labels);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WhenEnabled()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>
        {
            [("Alice", "Track", "")] = new() { Genres = "Trance", Labels = "Label" }
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/Alice - Track_x1.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("Trance", info.Genres);
        Assert.Equal("Label", info.Labels);
    }

    [Fact]
    public void TryResolveInfo_ReturnsFalse_WhenNoMatchAndFallbackDisabled()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>();

        var ok = TaggingResolver.TryResolveInfo("C:/music/Unknown - Song.mp3", "Unknown", "Song", "", db, allowFilenameFallback: false, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>
        {
            [("A", "T", "Club Mix")] = new() { Genres = "POP", Labels = "Sony" }
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Club Mix)_abc123.mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("POP", info.Genres);
    }

    [Fact]
    public void TryResolveInfo_UsesFilenameFallback_WithVersionlessFallbackFromVersionPattern()
    {
        var db = new Dictionary<(string a, string t, string v), TrackLookupInfo>
        {
            [("A", "T", "")] = new() { Genres = "House", Labels = "Armada" }
        };

        var ok = TaggingResolver.TryResolveInfo("C:/music/A - T (Unknown Mix).mp3", "", "", "", db, allowFilenameFallback: true, out var info);

        Assert.True(ok);
        Assert.Equal("House", info.Genres);
    }
}
