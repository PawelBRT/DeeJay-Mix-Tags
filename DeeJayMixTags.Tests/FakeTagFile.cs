using System;
using System.Collections.Generic;
using System.Linq;
using TagLib;

namespace Mp3TaggerGUI.Tests;

public sealed class FakeTagFile : TagLib.File
{
    private readonly FakeTag _tag;
    private readonly TagAdapter _adapter;

    public FakeTagFile(string artist, string title, string version, string genres, string publisher)
        : base("fake")
    {
        _tag = new FakeTag(artist, title, version, genres, publisher);
        _adapter = new TagAdapter(_tag);
    }

    public int SaveCount { get; private set; }

    public override TagLib.Tag Tag => _adapter;

    public override Properties Properties => null!;

    public override void Save()
    {
        SaveCount++;
    }

    public override TagLib.Tag GetTag(TagTypes type, bool create)
    {
        return _adapter;
    }

    public override void RemoveTags(TagTypes types)
    {
    }

    public FakeTag Id3v2 => _tag;

    public sealed class FakeTag
    {
        public string[] Performers { get; set; } = Array.Empty<string>();
        public string Title { get; set; } = "";
        public string[] Genres { get; set; } = Array.Empty<string>();
        public string Publisher { get; set; } = "";

        public string JoinedPerformers => string.Join("; ", Performers ?? Array.Empty<string>());

        private readonly List<TagLib.Id3v2.UserTextInformationFrame> _txxx = new();

        public FakeTag(string artist, string title, string version, string genres, string publisher)
        {
            Performers = string.IsNullOrWhiteSpace(artist) ? [] : [artist];
            Title = string.IsNullOrWhiteSpace(version) ? title : $"{title} ({version})";
            Genres = string.IsNullOrWhiteSpace(genres) ? [] : [genres];
            Publisher = publisher;
        }

        public void AddTxxxFrame(TagLib.Id3v2.UserTextInformationFrame frame)
        {
            _txxx.Add(frame);
        }

        public List<TagLib.Id3v2.UserTextInformationFrame> GetTxxxFrames()
        {
            return _txxx;
        }

        public IEnumerable<T> GetFrames<T>() where T : TagLib.Id3v2.Frame
        {
            return _txxx.OfType<T>();
        }
    }

    private sealed class TagAdapter : TagLib.Tag
    {
        private readonly FakeTag _inner;

        public TagAdapter(FakeTag inner)
        {
            _inner = inner;
        }

        public override void Clear()
        {
        }

        public override string[] Performers
        {
            get => _inner.Performers;
            set => _inner.Performers = value;
        }

        public override string Title
        {
            get => _inner.Title;
            set => _inner.Title = value;
        }

        public override string[] Genres
        {
            get => _inner.Genres;
            set => _inner.Genres = value;
        }

        public override string Publisher
        {
            get => _inner.Publisher;
            set => _inner.Publisher = value;
        }

        public override TagTypes TagTypes => TagTypes.Id3v2;
    }
}


