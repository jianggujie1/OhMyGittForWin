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
        var info = GetCurrentBranchInfo(repoPath);
        return (info.BranchName, info.Upstream);
    }

    public (string BranchName, string? Upstream, int AheadCount, int BehindCount) GetCurrentBranchInfo(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var head = repo.Head;
        string name = head?.FriendlyName ?? "(Detached)";
        string? upstream = head?.TrackedBranch?.FriendlyName;
        int ahead = 0;
        int behind = 0;
        if (head?.TrackingDetails != null)
        {
            ahead = head.TrackingDetails.AheadBy ?? 0;
            behind = head.TrackingDetails.BehindBy ?? 0;
        }
        return (name, upstream, ahead, behind);
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

        // Find unpushed commits on current branch (ahead of tracked upstream)
        var unpushedShas = new HashSet<string>();
        var head = repo.Head;
        if (head != null && head.TrackedBranch != null && head.Tip != null && head.TrackedBranch.Tip != null)
        {
            try
            {
                var aheadFilter = new CommitFilter
                {
                    IncludeReachableFrom = head.Tip,
                    ExcludeReachableFrom = head.TrackedBranch.Tip
                };
                foreach (var c in repo.Commits.QueryBy(aheadFilter))
                {
                    unpushedShas.Add(c.Sha);
                }
            }
            catch
            {
                // Fallback gracefully if branches have diverged
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
                BranchesAndTags = refLookup.TryGetValue(commit.Sha, out var refs) ? refs : new List<string>(),
                IsUnpushed = unpushedShas.Contains(commit.Sha)
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

    public IdeaFileDiffResult GetIdeaFileDiff(string repoPath, string relativePath, bool isStaged)
    {
        using var repo = new Repository(repoPath);
        string oldContent = string.Empty;
        string newContent = string.Empty;
        string gitPath = relativePath.Replace('\\', '/');

        var headCommit = repo.Head.Tip;
        if (headCommit != null)
        {
            var treeEntry = headCommit[gitPath];
            if (treeEntry != null && treeEntry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)treeEntry.Target;
                oldContent = blob.GetContentText();
            }
        }

        string fullPath = Path.Combine(repo.Info.WorkingDirectory, relativePath);
        if (File.Exists(fullPath))
        {
            try { newContent = File.ReadAllText(fullPath); } catch { newContent = string.Empty; }
        }

        string shortSha = headCommit != null ? headCommit.Sha.Substring(0, Math.Min(7, headCommit.Sha.Length)) : "HEAD";
        return _diffService.BuildIdeaDiff(relativePath, oldContent, newContent, shortSha, "Current version");
    }

    public IdeaFileDiffResult GetIdeaCommitFileDiff(string repoPath, string commitSha, string relativePath)
    {
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitSha);
        if (commit == null) return new IdeaFileDiffResult { FilePath = relativePath };

        string oldContent = string.Empty;
        string newContent = string.Empty;
        string gitPath = relativePath.Replace('\\', '/');

        var currentEntry = commit[gitPath];
        if (currentEntry != null && currentEntry.TargetType == TreeEntryTargetType.Blob)
        {
            newContent = ((Blob)currentEntry.Target).GetContentText();
        }

        var parent = commit.Parents.FirstOrDefault();
        if (parent != null)
        {
            var parentEntry = parent[gitPath];
            if (parentEntry != null && parentEntry.TargetType == TreeEntryTargetType.Blob)
            {
                oldContent = ((Blob)parentEntry.Target).GetContentText();
            }
        }

        string leftSha = parent != null ? parent.Sha.Substring(0, Math.Min(7, parent.Sha.Length)) : "Root";
        string rightSha = commit.Sha.Substring(0, Math.Min(7, commit.Sha.Length));
        return _diffService.BuildIdeaDiff(relativePath, oldContent, newContent, leftSha, $"Commit {rightSha}");
    }

    private static string GetGitExePath()
    {
        var commonPaths = new[]
        {
            @"C:\Program Files\Git\cmd\git.exe",
            @"C:\Program Files\Git\bin\git.exe",
            @"C:\Program Files (x86)\Git\cmd\git.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe")
        };
        foreach (var p in commonPaths)
        {
            if (File.Exists(p)) return p;
        }
        return "git";
    }

    private static async Task<(bool Success, string Output)> RunGitCommandAsync(string repoPath, string arguments, int timeoutMs = 60000)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = GetGitExePath(),
                Arguments = arguments,
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Allow Git Credential Manager to pop up UI / browser login if needed
            psi.EnvironmentVariables["GCM_INTERACTIVE"] = "always";

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            if (!process.Start())
            {
                return (false, "无法启动 git 进程");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return (false, "执行超时 (超过 60 秒)。如果正在等待身份验证弹窗，请直接在终端运行一次 git push 完成登录，或使用【绑定 Token】。");
            }

            string stdout = outputBuilder.ToString().Trim();
            string stderr = errorBuilder.ToString().Trim();
            string output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            if (!string.IsNullOrWhiteSpace(stderr) && !output.Contains(stderr))
            {
                output = $"{output}\n{stderr}".Trim();
            }

            return (process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool Success, string Output) RunGitCommand(string repoPath, string arguments, int timeoutMs = 60000)
    {
        return RunGitCommandAsync(repoPath, arguments, timeoutMs).GetAwaiter().GetResult();
    }

    public List<GitRemoteItem> GetRemotes(string repoPath)
    {
        using var repo = new Repository(repoPath);
        return repo.Network.Remotes.Select(r => new GitRemoteItem { Name = r.Name, Url = r.Url }).ToList();
    }

    public void AddRemote(string repoPath, string name, string url)
    {
        using var repo = new Repository(repoPath);
        repo.Network.Remotes.Add(name, url);
    }

    public void RemoveRemote(string repoPath, string name)
    {
        using var repo = new Repository(repoPath);
        repo.Network.Remotes.Remove(name);
    }

    public void SetRemoteUrl(string repoPath, string name, string newUrl)
    {
        RunGitCommand(repoPath, $"remote set-url {name} \"{newUrl}\"");
    }

    public void CreateBranch(string repoPath, string branchName, string? targetSha = null)
    {
        using var repo = new Repository(repoPath);
        var tip = targetSha != null ? repo.Lookup<Commit>(targetSha) : repo.Head.Tip;
        if (tip != null)
        {
            repo.CreateBranch(branchName, tip);
        }
    }

    public void CheckoutBranch(string repoPath, string branchName)
    {
        using var repo = new Repository(repoPath);
        var branch = repo.Branches[branchName];
        if (branch != null)
        {
            if (branch.IsRemote)
            {
                // Create or checkout local tracking branch
                string localName = branch.FriendlyName.Contains('/') 
                    ? branch.FriendlyName.Substring(branch.FriendlyName.IndexOf('/') + 1)
                    : branch.FriendlyName;
                var localBranch = repo.Branches[localName];
                if (localBranch == null)
                {
                    localBranch = repo.CreateBranch(localName, branch.Tip);
                    repo.Branches.Update(localBranch, b => b.TrackedBranch = branch.CanonicalName);
                }
                Commands.Checkout(repo, localBranch);
            }
            else
            {
                Commands.Checkout(repo, branch);
            }
        }
    }

    public void DeleteBranch(string repoPath, string branchName)
    {
        using var repo = new Repository(repoPath);
        var branch = repo.Branches[branchName];
        if (branch != null)
        {
            repo.Branches.Remove(branch);
        }
    }

    public (bool Success, string Output) PushBranch(string repoPath, string remoteName, string branchName, bool setUpstream = true)
    {
        return PushBranchAsync(repoPath, remoteName, branchName, setUpstream).GetAwaiter().GetResult();
    }

    public Task<(bool Success, string Output)> PushBranchAsync(string repoPath, string remoteName, string branchName, bool setUpstream = true)
    {
        string args = setUpstream ? $"push -u {remoteName} {branchName}" : $"push {remoteName} {branchName}";
        return RunGitCommandAsync(repoPath, args, 120000);
    }

    public (bool Success, string Output) PullBranch(string repoPath, string? remoteName = null, string? branchName = null)
    {
        return PullBranchAsync(repoPath, remoteName, branchName).GetAwaiter().GetResult();
    }

    public Task<(bool Success, string Output)> PullBranchAsync(string repoPath, string? remoteName = null, string? branchName = null)
    {
        string args = !string.IsNullOrWhiteSpace(remoteName) && !string.IsNullOrWhiteSpace(branchName)
            ? $"pull {remoteName} {branchName}"
            : "pull";
        return RunGitCommandAsync(repoPath, args, 120000);
    }

    public (bool Success, string Output) Fetch(string repoPath, string? remoteName = null)
    {
        return FetchAsync(repoPath, remoteName).GetAwaiter().GetResult();
    }

    public Task<(bool Success, string Output)> FetchAsync(string repoPath, string? remoteName = null)
    {
        string args = string.IsNullOrWhiteSpace(remoteName) ? "fetch --all --prune" : $"fetch {remoteName} --prune";
        return RunGitCommandAsync(repoPath, args, 60000);
    }

    public (bool Success, string Output) DeleteRemoteBranch(string repoPath, string remoteName, string branchName)
    {
        return RunGitCommand(repoPath, $"push {remoteName} --delete {branchName}");
    }
}
