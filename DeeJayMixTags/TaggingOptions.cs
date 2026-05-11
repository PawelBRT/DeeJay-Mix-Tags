namespace Mp3TaggerGUI
{
    public class TaggingOptions
    {
        public bool DoGenre { get; set; } = true;
        public bool DoLabel { get; set; } = true;
        public bool FilenameFallback { get; set; } = true;
        public bool AlwaysAppendToGenre { get; set; } = true;   // dopnij DJPromo.pl
        public bool RemoveWorldPoland { get; set; } = true;     // usuń Świat/Polska z GENRE
        public bool NormalizeSeparators { get; set; } = true;   // , ; / : -> |
        public bool TitleCase { get; set; } = true;             // każde słowo wielką literą
        public bool ForcePopUpper { get; set; } = true;         // POP wielkimi
        public bool Dedup { get; set; } = true;                 // USUŃ duplikaty (przełączalne)
        public bool WriteTxxxLabel { get; set; } = true;        // mirror do TXXX:LABEL
        public bool PrependNew { get; set; } = true;            // nowe wartości na początek
        public bool DryRun { get; set; } = false;               // bez zapisu

        // CSV / Backup
        public bool WriteCsvReport { get; set; } = true;        // generuj _tagger_report.csv
        public bool WritePerFileBackup { get; set; } = true;    // jeden backup JSON na sesję
    }
}
