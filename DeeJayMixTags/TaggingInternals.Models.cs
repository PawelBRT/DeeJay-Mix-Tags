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
}
