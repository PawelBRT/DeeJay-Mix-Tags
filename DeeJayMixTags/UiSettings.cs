namespace Mp3TaggerGUI
{
    public class UiSettings
    {
        public string? Mp3Dir { get; set; }
        public string? JsonPath { get; set; }
        public TagDataSource DataSource { get; set; } = TagDataSource.DjPromoJson;
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

        public DjoidGenreSource DjoidGenreSource { get; set; } = DjoidGenreSource.None;
        public GenreWriteMode DjoidGenreWriteMode { get; set; } = GenreWriteMode.Append;
        public bool WriteDjoidGenreTag { get; set; } = true;
        public bool WriteDjoidSubgenreTag { get; set; } = true;
        public bool WriteDjoidEnergyTag { get; set; } = true;
        public bool WriteDjoidDanceabilityTag { get; set; } = true;
        public bool WriteDjoidEmotionTag { get; set; } = true;
        public bool WriteDjoidKeyTag { get; set; } = true;
        public bool WriteDjoidBpmTag { get; set; } = true;
        public bool ScaleDjoidEnergyDanceToTen { get; set; } = true;

        public string GridPreset { get; set; } = "Extended";
        public bool GridChangesOnly { get; set; } = false;
        public string GridColumnLayout { get; set; } = "";
    }
}
