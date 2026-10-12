namespace OhMyGittForWin.Models;

public class GitBranchItem
{
    public required string Name { get; set; }
    public required string FriendlyName { get; set; }
    public bool IsCurrentHead { get; set; }
    public bool IsRemote { get; set; }
    public string? UpstreamBranchName { get; set; }
    public int AheadCount { get; set; }
    public int BehindCount { get; set; }
    public required string TipSha { get; set; }

    public bool HasAhead => AheadCount > 0;
    public bool HasBehind => BehindCount > 0;
    public bool HasSyncInfo => !IsRemote && !string.IsNullOrEmpty(UpstreamBranchName);
    public bool IsUpToDate => HasSyncInfo && AheadCount == 0 && BehindCount == 0;
}
