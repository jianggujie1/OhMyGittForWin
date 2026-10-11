using System.Collections.Generic;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public interface IGitService
{
    bool IsGitRepository(string path);
    string? DiscoverRepository(string path);
    (string BranchName, string? Upstream) GetCurrentBranch(string repoPath);
    List<GitBranchItem> GetBranches(string repoPath);
    List<GitCommitItem> GetCommitHistory(string repoPath, int limit = 300);
    List<GitStatusFileItem> GetStatus(string repoPath);
    void StageFile(string repoPath, string relativePath);
    void UnstageFile(string repoPath, string relativePath);
    void StageAll(string repoPath);
    void UnstageAll(string repoPath);
    void Commit(string repoPath, string message, string? authorName = null, string? authorEmail = null);
    FileDiffResult GetFileDiff(string repoPath, string relativePath, bool isStaged);
    FileDiffResult GetCommitFileDiff(string repoPath, string commitSha, string relativePath);
    List<string> GetChangedFilesForCommit(string repoPath, string commitSha);
}
