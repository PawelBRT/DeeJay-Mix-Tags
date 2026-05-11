namespace Mp3TaggerGUI
{
    public class UiSettings
    {
        public string? Mp3Dir { get; set; }
        public string? JsonPath { get; set; }
        public bool DoGenre { get; set; } = true;
        public bool DoLabel { get; set; } = true;
        public bool FilenameFallback { get; set; } = true;
        public bool PrependNew { get; set; } = true;
        public bool Dedup { get; set; } = true;
        public bool NormalizeSeparators { get; set; } = true;
        public bool TitleCase { get; set; } = true;
        public bool ForcePopUpper { get; set; } = true;
        public bool RemoveWorldPoland { get; set; } = true;
        public bool AlwaysAppendToGenre { get; set; } = true;
        public bool WriteTxxxLabel { get; set; } = true;
        public bool DryRun { get; set; } = false;
        public bool WriteCsvReport { get; set; } = true;
        public bool WritePerFileBackup { get; set; } = true;
    }
}
