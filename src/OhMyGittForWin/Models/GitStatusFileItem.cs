using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace OhMyGittForWin.Models;

public enum GitFileStatusType
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted
}

public partial class GitStatusFileItem : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked = true;

    public required string FilePath { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public GitFileStatusType Status { get; set; }
    public bool IsStaged { get; set; }
    public bool IsConflicted => Status == GitFileStatusType.Conflicted;

    public string StatusBadge => Status switch
    {
        GitFileStatusType.Modified => "M",
        GitFileStatusType.Added => "A",
        GitFileStatusType.Deleted => "D",
        GitFileStatusType.Renamed => "R",
        GitFileStatusType.Untracked => "?",
        GitFileStatusType.Conflicted => "!",
        _ => " "
    };

    public string StatusColor => Status switch
    {
        GitFileStatusType.Modified => "#38BDF8",   // Light blue
        GitFileStatusType.Added => "#10B981",      // Emerald green
        GitFileStatusType.Deleted => "#EF4444",    // Rose red
        GitFileStatusType.Renamed => "#A78BFA",    // Purple
        GitFileStatusType.Untracked => "#34D399",  // Mint green
        GitFileStatusType.Conflicted => "#F97316", // Amber / Orange
        _ => "#94A3B8"
    };

    public string StagedLabel => IsStaged ? "已暂存 (Staged)" : "未暂存 (Unstaged)";
    public string StagedTagColor => IsStaged ? "#10B981" : "#F59E0B";
}
