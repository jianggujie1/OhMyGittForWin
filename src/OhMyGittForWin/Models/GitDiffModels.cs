using System.Collections.Generic;
using DiffPlex.DiffBuilder.Model;

namespace OhMyGittForWin.Models;

public enum DiffLineType
{
    Unchanged,
    Inserted,
    Deleted,
    Modified,
    Header,
    Imaginary
}

public class DiffLineModel
{
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public required string Text { get; set; }
    public DiffLineType Type { get; set; }
}

public class SideBySideDiffRow
{
    public int RowIndex { get; set; }
    
    // Left side (Old / Base)
    public int? OldLineNumber { get; set; }
    public string OldText { get; set; } = string.Empty;
    public ChangeType OldType { get; set; } = ChangeType.Unchanged;

    // Right side (New / Working copy)
    public int? NewLineNumber { get; set; }
    public string NewText { get; set; } = string.Empty;
    public ChangeType NewType { get; set; } = ChangeType.Unchanged;

    public bool IsDifference => OldType != ChangeType.Unchanged || NewType != ChangeType.Unchanged;
}

public class FileDiffResult
{
    public required string FilePath { get; set; }
    public string OldText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
    public List<DiffLineModel> Lines { get; set; } = new();
    public List<SideBySideDiffRow> SideBySideRows { get; set; } = new();
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int Modifications { get; set; }
    public int TotalDifferences => Additions + Deletions + Modifications;
}
