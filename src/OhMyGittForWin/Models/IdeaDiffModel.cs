using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiffPlex.DiffBuilder.Model;

namespace OhMyGittForWin.Models;

public class IdeaWordPiece
{
    public required string Text { get; set; }
    public bool IsHighlighted { get; set; }
}

public class IdeaDiffLine
{
    public int? LineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public ChangeType Type { get; set; } = ChangeType.Unchanged;
    public List<IdeaWordPiece> WordPieces { get; set; } = new();
}

public partial class IdeaDiffHunk : ObservableObject
{
    public int HunkId { get; set; }
    public ChangeType Type { get; set; } = ChangeType.Unchanged;

    public ObservableCollection<IdeaDiffLine> LeftLines { get; set; } = new();
    public ObservableCollection<IdeaDiffLine> RightLines { get; set; } = new();

    [ObservableProperty]
    private bool _isIncluded = false;

    public bool HasDifferences => Type != ChangeType.Unchanged;

    // Visual dimensions for the ribbon connector
    public int LeftLineCount => LeftLines.Count;
    public int RightLineCount => RightLines.Count;

    public string LeftLineRangeDisplay
    {
        get
        {
            if (LeftLines.Count == 0) return "";
            var first = LeftLines[0].LineNumber;
            var last = LeftLines[^1].LineNumber;
            return first == last ? $"{first}" : $"{first}-{last}";
        }
    }

    public string RightLineRangeDisplay
    {
        get
        {
            if (RightLines.Count == 0) return "";
            var first = RightLines[0].LineNumber;
            var last = RightLines[^1].LineNumber;
            return first == last ? $"{first}" : $"{first}-{last}";
        }
    }

    public string TypeBadgeText => Type switch
    {
        ChangeType.Inserted => $"+{RightLineCount} 新增",
        ChangeType.Deleted => $"-{LeftLineCount} 删除",
        ChangeType.Modified => $"{LeftLineCount} ➔ {RightLineCount} 修改",
        _ => ""
    };

    public string TypeBadgeBackground => Type switch
    {
        ChangeType.Inserted => "#166534",
        ChangeType.Deleted => "#991B1B",
        ChangeType.Modified => "#1E40AF",
        _ => "Transparent"
    };
}

public class IdeaFileDiffResult
{
    public required string FilePath { get; set; }
    public string LeftCommitSha { get; set; } = "HEAD";
    public string RightTitle { get; set; } = "Current version";
    public ObservableCollection<IdeaDiffHunk> Hunks { get; set; } = new();

    public int TotalDifferences { get; set; }
    public int IncludedCount { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int Modifications { get; set; }
}
