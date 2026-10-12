using System;
using System.Collections.Generic;

namespace OhMyGittForWin.Models;

public class GitCommitItem
{
    public required string Sha { get; set; }
    public required string ShortSha { get; set; }
    public required string Message { get; set; }
    public string MessageShort => Message.Split('\n')[0].Trim();
    public required string AuthorName { get; set; }
    public required string AuthorEmail { get; set; }
    public DateTimeOffset AuthorDate { get; set; }
    public string FormattedDate => AuthorDate.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public List<string> ParentShas { get; set; } = new();
    public List<string> BranchesAndTags { get; set; } = new();
    public bool HasRefs => BranchesAndTags.Count > 0;
    public bool IsUnpushed { get; set; }
}
