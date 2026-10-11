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
}
