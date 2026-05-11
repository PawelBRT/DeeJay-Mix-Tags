using TagLib;

namespace Mp3TaggerGUI.Tests;

public sealed class FakeTagFile : TagLib.File
{
    private readonly FakeTag _tag;

    public FakeTagFile(string artist, string title, string version, string genres, string publisher)
        : base("fake")
    {
        _tag = new FakeTag(artist, title, version, genres, publisher);
    }

    public int SaveCount { get; private set; }

    public override TagLib.Tag Tag => _tag;

    public override Properties Properties => null!;

    public override void Save()
    {
        SaveCount++;
    }

    public override TagLib.Tag GetTag(TagTypes type, bool create)
    {
        return _tag;
    }

    public override void RemoveTags(TagTypes types)
    {
    }

    public FakeTag Id3v2 => _tag;

    public sealed class FakeTag : TagLib.Id3v2.Tag
    {
        public FakeTag(string artist, string title, string version, string genres, string publisher)
        {
            Performers = [artist];
            Title = string.IsNullOrWhiteSpace(version) ? title : $"{title} ({version})";
            Genres = [genres];
            Publisher = publisher;
        }
    }
}
