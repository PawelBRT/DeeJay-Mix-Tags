namespace Mp3TaggerGUI
{
    public enum TagDataSource
    {
        DjPromoJson,
        DjoidJson
    }

    public enum DjoidGenreSource
    {
        None,
        GenreOnly,
        SubgenreOnly,
        GenreAndSubgenre
    }

    public enum GenreWriteMode
    {
        Replace,
        Append,
        Prepend
    }

    public class TaggingOptions
    {
        public TagDataSource DataSource { get; set; } = TagDataSource.DjPromoJson;
        public bool DoGenre { get; set; } = true;
        public bool DoLabel { get; set; } = true;
        public bool FilenameFallback { get; set; } = true;
        public bool AlwaysAppendToGenre { get; set; } = true;
        public bool RemoveWorldPoland { get; set; } = true;
        public bool NormalizeSeparators { get; set; } = true;
        public bool TitleCase { get; set; } = true;
        public bool ForcePopUpper { get; set; } = true;
        public bool Dedup { get; set; } = true;
        public bool WriteTxxxLabel { get; set; } = true;
        public bool PrependNew { get; set; } = true;
        public bool DryRun { get; set; } = false;

        public bool WriteCsvReport { get; set; } = true;
        public bool WritePerFileBackup { get; set; } = true;
        public bool WriteDmcComment { get; set; } = false;
        public bool RepairDmcComment { get; set; } = false;
        public bool CleanupCommentMetadata { get; set; } = false;
        public bool WriteDjoidComment { get; set; } = false;
        public bool WriteDmcGenreTag { get; set; } = false;

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
    }
}
