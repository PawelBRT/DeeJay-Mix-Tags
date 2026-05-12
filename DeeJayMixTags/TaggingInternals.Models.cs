namespace Mp3TaggerGUI
{
    internal enum ChangeKind
    {
        Updated,
        Unchanged,
        Missing
    }

    internal sealed class ChangeRecord
    {
        public ChangeKind Kind { get; set; }
        public string BeforeGenre { get; set; } = "";
        public string AfterGenre { get; set; } = "";
        public string BeforeLabel { get; set; } = "";
        public string AfterLabel { get; set; } = "";
    }

    internal sealed class TrackLookupInfo
    {
        public string Genres { get; set; } = "";
        public string Labels { get; set; } = "";
        public string DjoidGenre { get; set; } = "";
        public string DjoidSubgenre { get; set; } = "";
        public string DjoidEnergy { get; set; } = "";
        public string DjoidDanceability { get; set; } = "";
        public string DjoidEmotion { get; set; } = "";
        public string DjoidKey { get; set; } = "";
        public string DjoidBpm { get; set; } = "";
    }

    public sealed class TagEditRow
    {
        public bool Apply { get; set; } = true;
        public bool IsDjoid { get; set; }
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string Version { get; set; } = "";
        public string CurrentAlbum { get; set; } = "";
        public string Album { get; set; } = "";
        public string CurrentYear { get; set; } = "";
        public string Year { get; set; } = "";
        public string CurrentTrack { get; set; } = "";
        public string Track { get; set; } = "";
        public string CurrentBpm { get; set; } = "";
        public string Bpm { get; set; } = "";
        public string CurrentKey { get; set; } = "";
        public string Key { get; set; } = "";
        public string CurrentComment { get; set; } = "";
        public string Comment { get; set; } = "";
        public string CurrentGenre { get; set; } = "";
        public string CurrentLabel { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Label { get; set; } = "";
        public string DjoidGenre { get; set; } = "";
        public string DjoidSubgenre { get; set; } = "";
        public string DjoidEnergy { get; set; } = "";
        public string DjoidDanceability { get; set; } = "";
        public string DjoidEmotion { get; set; } = "";
        public string DjoidKey { get; set; } = "";
        public string DjoidBpm { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
