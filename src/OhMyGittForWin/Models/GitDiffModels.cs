using System.Collections.Generic;

namespace OhMyGittForWin.Models;

public enum DiffLineType
{
    Unchanged,
    Inserted,
    Deleted,
    Header
}

public class DiffLineModel
{
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public required string Text { get; set; }
    public DiffLineType Type { get; set; }
}

public class FileDiffResult
{
    public required string FilePath { get; set; }
    public string OldText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
    public List<DiffLineModel> Lines { get; set; } = new();
    public int Additions { get; set; }
    public int Deletions { get; set; }
}
