using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public class GitService : IGitService
{
    private readonly DiffService _diffService = new();

    public bool IsGitRepository(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && Repository.IsValid(path);
    }

    public string? DiscoverRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Repository.Discover(path);
    }

    public (string BranchName, string? Upstream) GetCurrentBranch(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var head = repo.Head;
        string name = head?.FriendlyName ?? "(Detached)";
        string? upstream = head?.TrackedBranch?.FriendlyName;
        return (name, upstream);
    }

    public List<GitBranchItem> GetBranches(string repoPath)
    {
        var result = new List<GitBranchItem>();
        using var repo = new Repository(repoPath);

        foreach (var branch in repo.Branches)
        {
            var item = new GitBranchItem
            {
                Name = branch.CanonicalName,
                FriendlyName = branch.FriendlyName,
                IsCurrentHead = branch.IsCurrentRepositoryHead,
                IsRemote = branch.IsRemote,
                UpstreamBranchName = branch.TrackedBranch?.FriendlyName,
                TipSha = branch.Tip?.Sha ?? string.Empty
            };

            if (branch.TrackingDetails != null)
            {
                item.AheadCount = branch.TrackingDetails.AheadBy ?? 0;
                item.BehindCount = branch.TrackingDetails.BehindBy ?? 0;
            }

            result.Add(item);
        }

        return result.OrderByDescending(b => b.IsCurrentHead).ThenBy(b => b.IsRemote).ThenBy(b => b.FriendlyName).ToList();
    }

    public List<GitCommitItem> GetCommitHistory(string repoPath, int limit = 300)
    {
        var result = new List<GitCommitItem>();
        using var repo = new Repository(repoPath);

        if (!repo.Commits.Any()) return result;

        // Build ref lookup for tags and branches pointing to commits
        var refLookup = new Dictionary<string, List<string>>();
        foreach (var b in repo.Branches)
        {
            if (b.Tip != null)
            {
                if (!refLookup.ContainsKey(b.Tip.Sha)) refLookup[b.Tip.Sha] = new List<string>();
                refLookup[b.Tip.Sha].Add(b.FriendlyName);
            }
        }
        foreach (var t in repo.Tags)
        {
            var targetSha = t.Target is Commit c ? c.Sha : (t.PeeledTarget is Commit pc ? pc.Sha : null);
            if (targetSha != null)
            {
                if (!refLookup.ContainsKey(targetSha)) refLookup[targetSha] = new List<string>();
                refLookup[targetSha].Add("tag: " + t.FriendlyName);
            }
        }

        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
            IncludeReachableFrom = repo.Refs.Where(r => r.IsLocalBranch || r.IsRemoteTrackingBranch).Select(r => (object)r.CanonicalName).ToList()
        };

        var commits = repo.Commits.QueryBy(filter).Take(limit);
        foreach (var commit in commits)
        {
            var item = new GitCommitItem
            {
                Sha = commit.Sha,
                ShortSha = commit.Sha.Substring(0, System.Math.Min(7, commit.Sha.Length)),
                Message = commit.Message,
                AuthorName = commit.Author.Name,
                AuthorEmail = commit.Author.Email,
                AuthorDate = commit.Author.When,
                ParentShas = commit.Parents.Select(p => p.Sha).ToList(),
                BranchesAndTags = refLookup.TryGetValue(commit.Sha, out var refs) ? refs : new List<string>()
            };
            result.Add(item);
        }

        return result;
    }

    public List<GitStatusFileItem> GetStatus(string repoPath)
    {
        var result = new List<GitStatusFileItem>();
        using var repo = new Repository(repoPath);

        var status = repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });

        foreach (var entry in status)
        {
            if (entry.State.HasFlag(FileStatus.Conflicted))
            {
                result.Add(new GitStatusFileItem
                {
                    FilePath = entry.FilePath,
                    Status = GitFileStatusType.Conflicted,
                    IsStaged = false
                });
            }
            else
            {
                if (entry.State.HasFlag(FileStatus.NewInIndex) ||
                    entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
                    entry.State.HasFlag(FileStatus.DeletedFromIndex) ||
                    entry.State.HasFlag(FileStatus.RenamedInIndex))
                {
                    result.Add(new GitStatusFileItem
                    {
                        FilePath = entry.FilePath,
                        Status = entry.State.HasFlag(FileStatus.NewInIndex) ? GitFileStatusType.Added :
                                 entry.State.HasFlag(FileStatus.DeletedFromIndex) ? GitFileStatusType.Deleted :
                                 entry.State.HasFlag(FileStatus.RenamedInIndex) ? GitFileStatusType.Renamed :
                                 GitFileStatusType.Modified,
                        IsStaged = true
                    });
                }

                if (entry.State.HasFlag(FileStatus.NewInWorkdir) ||
                    entry.State.HasFlag(FileStatus.ModifiedInWorkdir) ||
                    entry.State.HasFlag(FileStatus.DeletedFromWorkdir) ||
                    entry.State.HasFlag(FileStatus.RenamedInWorkdir))
                {
                    result.Add(new GitStatusFileItem
                    {
                        FilePath = entry.FilePath,
                        Status = entry.State.HasFlag(FileStatus.NewInWorkdir) ? GitFileStatusType.Untracked :
                                 entry.State.HasFlag(FileStatus.DeletedFromWorkdir) ? GitFileStatusType.Deleted :
                                 entry.State.HasFlag(FileStatus.RenamedInWorkdir) ? GitFileStatusType.Renamed :
                                 GitFileStatusType.Modified,
                        IsStaged = false
                    });
                }
            }
        }

        return result.OrderBy(x => x.FilePath).ToList();
    }

    public void StageFile(string repoPath, string relativePath)
    {
        using var repo = new Repository(repoPath);
        Commands.Stage(repo, relativePath);
    }

    public void UnstageFile(string repoPath, string relativePath)
    {
        using var repo = new Repository(repoPath);
        Commands.Unstage(repo, relativePath);
    }

    public void StageAll(string repoPath)
    {
        using var repo = new Repository(repoPath);
        Commands.Stage(repo, "*");
    }

    public void UnstageAll(string repoPath)
    {
        using var repo = new Repository(repoPath);
        Commands.Unstage(repo, "*");
    }

    public void Commit(string repoPath, string message, string? authorName = null, string? authorEmail = null)
    {
        using var repo = new Repository(repoPath);
        var config = repo.Config;
        string name = authorName ?? config.Get<string>("user.name")?.Value ?? "User";
        string email = authorEmail ?? config.Get<string>("user.email")?.Value ?? "user@example.com";
        var sig = new Signature(name, email, DateTimeOffset.Now);

        repo.Commit(message, sig, sig);
    }

    public FileDiffResult GetFileDiff(string repoPath, string relativePath, bool isStaged)
    {
        using var repo = new Repository(repoPath);
        string oldContent = string.Empty;
        string newContent = string.Empty;

        var headCommit = repo.Head.Tip;
        if (headCommit != null)
        {
            var treeEntry = headCommit[relativePath];
            if (treeEntry != null && treeEntry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)treeEntry.Target;
                oldContent = blob.GetContentText();
            }
        }

        string fullPath = Path.Combine(repo.Info.WorkingDirectory, relativePath);
        if (File.Exists(fullPath))
        {
            try
            {
                newContent = File.ReadAllText(fullPath);
            }
            catch
            {
                newContent = string.Empty;
            }
        }

        return _diffService.BuildDiff(relativePath, oldContent, newContent);
    }

    public FileDiffResult GetCommitFileDiff(string repoPath, string commitSha, string relativePath)
    {
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitSha);
        if (commit == null) return new FileDiffResult { FilePath = relativePath };

        string oldContent = string.Empty;
        string newContent = string.Empty;

        var currentEntry = commit[relativePath];
        if (currentEntry != null && currentEntry.TargetType == TreeEntryTargetType.Blob)
        {
            newContent = ((Blob)currentEntry.Target).GetContentText();
        }

        var parent = commit.Parents.FirstOrDefault();
        if (parent != null)
        {
            var parentEntry = parent[relativePath];
            if (parentEntry != null && parentEntry.TargetType == TreeEntryTargetType.Blob)
            {
                oldContent = ((Blob)parentEntry.Target).GetContentText();
            }
        }

        return _diffService.BuildDiff(relativePath, oldContent, newContent);
    }

    public List<string> GetChangedFilesForCommit(string repoPath, string commitSha)
    {
        var result = new List<string>();
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitSha);
        if (commit == null) return result;

        var parent = commit.Parents.FirstOrDefault();
        var oldTree = parent?.Tree;
        var newTree = commit.Tree;

        var patch = repo.Diff.Compare<Patch>(oldTree, newTree);
        foreach (var change in patch)
        {
            result.Add(change.Path);
        }

        return result;
    }
}
