using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OhMyGittForWin.Models;
using OhMyGittForWin.Services;
using OhMyGittForWin.Views;

namespace OhMyGittForWin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private List<GitCommitItem> _allCommits = new();
    private CancellationTokenSource? _commitSelectionCts;
    private CancellationTokenSource? _commitDiffCts;
    private CancellationTokenSource? _statusDiffCts;
    private CancellationTokenSource? _filterCts;

    [ObservableProperty]
    private string _repoPath = string.Empty;

    [ObservableProperty]
    private string _repoName = "未打开仓库";

    [ObservableProperty]
    private string _currentBranch = "-";

    [ObservableProperty]
    private string? _currentUpstream;

    [ObservableProperty]
    private int _currentAheadCount;

    [ObservableProperty]
    private int _currentBehindCount;

    [ObservableProperty]
    private bool _hasCurrentUpstream;

    [ObservableProperty]
    private string _syncStatusText = "未关联远程";

    [ObservableProperty]
    private string _syncStatusToolTip = string.Empty;

    public bool HasAheadCommits => CurrentAheadCount > 0;
    public bool HasBehindCommits => CurrentBehindCount > 0;
    public bool IsUpToDate => HasCurrentUpstream && CurrentAheadCount == 0 && CurrentBehindCount == 0;
    public bool HasNoUpstream => !HasCurrentUpstream;

    [ObservableProperty]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _filterKeyword = string.Empty;

    [ObservableProperty]
    private GitCommitItem? _selectedCommit;

    [ObservableProperty]
    private string? _selectedCommitFile;

    [ObservableProperty]
    private GitStatusFileItem? _selectedStatusFile;

    [ObservableProperty]
    private FileDiffResult? _currentDiff;

    [ObservableProperty]
    private IdeaFileDiffResult? _currentIdeaDiff;

    [ObservableProperty]
    private IdeaFileDiffResult? _statusIdeaDiff;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0: Log, 1: Commit

    public ObservableCollection<GitBranchItem> Branches { get; } = new();
    public ObservableCollection<GitCommitItem> FilteredCommits { get; } = new();
    public ObservableCollection<string> CommitFiles { get; } = new();
    public ObservableCollection<GitStatusFileItem> StatusFiles { get; } = new();

    public bool IsWorkingTreeClean => StatusFiles.Count == 0;
    public bool HasChangesToCommit => StatusFiles.Count > 0;

    [ObservableProperty]
    private string _gpuAccelerationStatus = string.Empty;

    public MainViewModel(IGitService gitService)
    {
        _gitService = gitService;

        int tier = (System.Windows.Media.RenderCapability.Tier >> 16);
        GpuAccelerationStatus = tier switch
        {
            2 => "⚡ GPU 加速: Tier 2 (Direct3D 硬件渲染)",
            1 => "⚡ GPU 加速: Tier 1 (部分硬件加速)",
            _ => "⚠️ 渲染模式: Tier 0 (软件光栅化)"
        };
    }

    public MainViewModel() : this(new GitService())
    {
    }

    [RelayCommand]
    public async Task OpenRepositoryAsync(string? folderPath = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择 Git 仓库目录"
            };

            if (dialog.ShowDialog() == true)
            {
                folderPath = dialog.FolderName;
            }
            else
            {
                return;
            }
        }

        string? discovered = _gitService.DiscoverRepository(folderPath);
        if (string.IsNullOrEmpty(discovered))
        {
            MessageBox.Show($"目录 '{folderPath}' 不是有效的 Git 仓库！", "打开仓库失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RepoPath = Path.GetDirectoryName(discovered.TrimEnd('\\', '/')) ?? discovered;
        RepoName = Path.GetFileName(RepoPath);

        await RefreshRepositoryAsync();
    }

    [RelayCommand]
    public async Task RefreshRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath)) return;

        IsLoading = true;
        StatusMessage = "正在读取仓库状态与提交历史...";

        try
        {
            await Task.Run(() =>
            {
                var (branch, upstream, ahead, behind) = _gitService.GetCurrentBranchInfo(RepoPath);
                var branchList = _gitService.GetBranches(RepoPath);
                var statusList = _gitService.GetStatus(RepoPath);
                var commitList = _gitService.GetCommitHistory(RepoPath, 500);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentBranch = branch;
                    CurrentUpstream = upstream;
                    CurrentAheadCount = ahead;
                    CurrentBehindCount = behind;
                    HasCurrentUpstream = !string.IsNullOrEmpty(upstream);

                    if (!HasCurrentUpstream)
                    {
                        SyncStatusText = "未追踪远程分支";
                        SyncStatusToolTip = "当前分支尚未建立远程追踪分支。点击【⬆ 推送 (Push)】可自动在远程创建并建立追踪关系。";
                    }
                    else if (ahead > 0 && behind > 0)
                    {
                        SyncStatusText = $"↑ {ahead} 待推送 · ↓ {behind} 待拉取";
                        SyncStatusToolTip = $"分支已分叉！\n领先远程 {upstream} 共 {ahead} 个提交（待推送）\n落后远程 {upstream} 共 {behind} 个提交（待拉取更新）\n建议先执行【⬇ 拉取 (Pull)】合并最新代码。";
                    }
                    else if (ahead > 0)
                    {
                        SyncStatusText = $"↑ {ahead} 待推送";
                        SyncStatusToolTip = $"当前分支领先远程 {upstream} 共 {ahead} 个提交。\n点击【⬆ 推送 (Push)】可发布到远程仓库。";
                    }
                    else if (behind > 0)
                    {
                        SyncStatusText = $"↓ {behind} 待拉取";
                        SyncStatusToolTip = $"远程 {upstream} 有 {behind} 个新提交。\n点击【⬇ 拉取 (Pull)】可更新到本地。";
                    }
                    else
                    {
                        SyncStatusText = $"已与 {upstream} 同步";
                        SyncStatusToolTip = $"本地分支与远程 {upstream} 保持最新同步，无需推送或拉取。\n点击可重新抓取远程 (Fetch) 检查更新。";
                    }

                    OnPropertyChanged(nameof(HasAheadCommits));
                    OnPropertyChanged(nameof(HasBehindCommits));
                    OnPropertyChanged(nameof(IsUpToDate));
                    OnPropertyChanged(nameof(HasNoUpstream));

                    Branches.Clear();
                    foreach (var b in branchList) Branches.Add(b);

                    string? previousSelectedFilePath = SelectedStatusFile?.FilePath;
                    StatusFiles.Clear();
                    foreach (var s in statusList) StatusFiles.Add(s);

                    if (StatusFiles.Count > 0)
                    {
                        var match = !string.IsNullOrEmpty(previousSelectedFilePath)
                            ? StatusFiles.FirstOrDefault(f => f.FilePath == previousSelectedFilePath)
                            : null;
                        SelectedStatusFile = match ?? StatusFiles[0];
                    }
                    else
                    {
                        SelectedStatusFile = null;
                        StatusIdeaDiff = null;
                    }

                    OnPropertyChanged(nameof(IsWorkingTreeClean));
                    OnPropertyChanged(nameof(HasChangesToCommit));

                    _allCommits = commitList;
                    ApplyCommitFilter();

                    StatusMessage = $"已加载 {Branches.Count} 个分支，{StatusFiles.Count} 个变更文件，{_allCommits.Count} 次提交";
                });
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新出错: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFilterKeywordChanged(string value)
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        var cts = new CancellationTokenSource();
        _filterCts = cts;

        _ = Task.Run(async () =>
        {
            await Task.Delay(120, cts.Token);
            if (cts.Token.IsCancellationRequested) return;

            var query = _allCommits.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(value))
            {
                query = query.Where(c =>
                    c.Message.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                    c.AuthorName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                    c.ShortSha.Contains(value, StringComparison.OrdinalIgnoreCase));
            }
            var filtered = query.ToList();

            if (cts.Token.IsCancellationRequested) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (cts.Token.IsCancellationRequested) return;
                FilteredCommits.Clear();
                foreach (var c in filtered) FilteredCommits.Add(c);

                if (FilteredCommits.Count > 0 && (SelectedCommit == null || !FilteredCommits.Contains(SelectedCommit)))
                {
                    SelectedCommit = FilteredCommits[0];
                }
            });
        }, cts.Token);
    }

    private void ApplyCommitFilter()
    {
        FilteredCommits.Clear();
        var query = _allCommits.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(FilterKeyword))
        {
            query = query.Where(c =>
                c.Message.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase) ||
                c.AuthorName.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase) ||
                c.ShortSha.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var c in query)
        {
            FilteredCommits.Add(c);
        }

        if (FilteredCommits.Count > 0 && SelectedCommit == null)
        {
            SelectedCommit = FilteredCommits[0];
        }
    }

    partial void OnSelectedCommitChanged(GitCommitItem? value)
    {
        _commitSelectionCts?.Cancel();
        _commitSelectionCts?.Dispose();
        _commitSelectionCts = null;

        _commitDiffCts?.Cancel();
        _commitDiffCts?.Dispose();
        _commitDiffCts = null;

        CommitFiles.Clear();
        CurrentDiff = null;
        CurrentIdeaDiff = null;
        _selectedCommitFile = null;
        OnPropertyChanged(nameof(SelectedCommitFile));

        if (value == null || string.IsNullOrWhiteSpace(RepoPath)) return;

        var repoPath = RepoPath;
        var sha = value.Sha;
        var cts = new CancellationTokenSource();
        _commitSelectionCts = cts;

        _ = Task.Run(() =>
        {
            if (cts.Token.IsCancellationRequested) return;

            try
            {
                var files = _gitService.GetChangedFilesForCommit(repoPath, sha);
                if (cts.Token.IsCancellationRequested) return;

                IdeaFileDiffResult? firstDiff = null;
                string? firstFile = files.FirstOrDefault();
                if (firstFile != null)
                {
                    firstDiff = _gitService.GetIdeaCommitFileDiff(repoPath, sha, firstFile);
                }

                if (cts.Token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (cts.Token.IsCancellationRequested) return;

                    CommitFiles.Clear();
                    foreach (var f in files) CommitFiles.Add(f);

                    if (firstFile != null)
                    {
                        _selectedCommitFile = firstFile;
                        OnPropertyChanged(nameof(SelectedCommitFile));
                        CurrentIdeaDiff = firstDiff;
                    }
                });
            }
            catch (Exception ex)
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"读取提交详情失败: {ex.Message}";
                    });
                }
            }
        }, cts.Token);
    }

    partial void OnSelectedCommitFileChanged(string? value)
    {
        _commitDiffCts?.Cancel();
        _commitDiffCts?.Dispose();
        _commitDiffCts = null;

        if (value == null || SelectedCommit == null || string.IsNullOrWhiteSpace(RepoPath))
        {
            CurrentIdeaDiff = null;
            return;
        }

        var repoPath = RepoPath;
        var commitSha = SelectedCommit.Sha;
        var filePath = value;
        var cts = new CancellationTokenSource();
        _commitDiffCts = cts;

        _ = Task.Run(() =>
        {
            if (cts.Token.IsCancellationRequested) return;
            try
            {
                var ideaDiff = _gitService.GetIdeaCommitFileDiff(repoPath, commitSha, filePath);
                if (cts.Token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (cts.Token.IsCancellationRequested) return;
                    CurrentIdeaDiff = ideaDiff;
                });
            }
            catch (Exception ex)
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"读取 Diff 失败: {ex.Message}";
                    });
                }
            }
        }, cts.Token);
    }

    partial void OnSelectedStatusFileChanged(GitStatusFileItem? value)
    {
        _statusDiffCts?.Cancel();
        _statusDiffCts?.Dispose();
        _statusDiffCts = null;

        if (value == null)
        {
            StatusIdeaDiff = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(RepoPath))
        {
            StatusIdeaDiff = null;
            return;
        }

        var repoPath = RepoPath;
        var filePath = value.FilePath;
        var isStaged = value.IsStaged;
        var cts = new CancellationTokenSource();
        _statusDiffCts = cts;

        _ = Task.Run(() =>
        {
            if (cts.Token.IsCancellationRequested) return;
            try
            {
                var ideaDiff = _gitService.GetIdeaFileDiff(repoPath, filePath, isStaged);
                if (cts.Token.IsCancellationRequested) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (cts.Token.IsCancellationRequested) return;
                    StatusIdeaDiff = ideaDiff;
                });
            }
            catch (Exception ex)
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"读取文件变更失败: {ex.Message}";
                    });
                }
            }
        }, cts.Token);
    }

    [RelayCommand]
    public void StageFile(GitStatusFileItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(RepoPath)) return;
        try
        {
            _gitService.StageFile(RepoPath, item.FilePath);
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "暂存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void UnstageFile(GitStatusFileItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(RepoPath)) return;
        try
        {
            _gitService.UnstageFile(RepoPath, item.FilePath);
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "取消暂存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void StageAll()
    {
        if (string.IsNullOrWhiteSpace(RepoPath)) return;
        try
        {
            _gitService.StageAll(RepoPath);
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "暂存全部失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void UnstageAll()
    {
        if (string.IsNullOrWhiteSpace(RepoPath)) return;
        try
        {
            _gitService.UnstageAll(RepoPath);
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "取消全部暂存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void CommitChanges()
    {
        if (string.IsNullOrWhiteSpace(RepoPath)) return;
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            MessageBox.Show("请输入 Commit 提交信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            // Auto stage checked files if none staged
            var checkedItems = StatusFiles.Where(x => x.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("请勾选需要提交的文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in checkedItems)
            {
                _gitService.StageFile(RepoPath, item.FilePath);
            }

            _gitService.Commit(RepoPath, CommitMessage.Trim());
            CommitMessage = string.Empty;
            StatusMessage = "提交成功！";
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "提交失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task PushAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath))
        {
            MessageBox.Show("请先打开一个 Git 仓库！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var remotes = _gitService.GetRemotes(RepoPath);
        if (remotes.Count == 0)
        {
            var res = MessageBox.Show("当前仓库尚未配置远程源！\n\n是否立即打开【远程管理】进行配置？", "未配置远程仓库", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                ManageRemotes();
            }
            return;
        }

        string remoteName = remotes.FirstOrDefault(r => r.Name == "origin")?.Name ?? remotes[0].Name;

        IsLoading = true;
        StatusMessage = $"正在推送分支 [{CurrentBranch}] 到远程 [{remoteName}]...";

        try
        {
            var (success, output) = await _gitService.PushBranchAsync(RepoPath, remoteName, CurrentBranch, true);
            if (success)
            {
                await _gitService.FetchAsync(RepoPath, remoteName);
                StatusMessage = $"🎉 成功推送到远程分支 {remoteName}/{CurrentBranch}！";
                MessageBox.Show($"推送成功！\n\n远程分支: {remoteName}/{CurrentBranch}\n\n输出详情:\n{output}", "推送成功", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshRepositoryAsync();
            }
            else
            {
                StatusMessage = $"推送失败: {output}";
                string tip = "";
                if (output.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("fatal: could not read Username", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Support for password authentication was removed", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Permission to", StringComparison.OrdinalIgnoreCase))
                {
                    tip = "\n\n💡 身份验证提示：\nGitHub 已于 2021 年停用账号密码推送。\n请点击顶部【🌐 远程管理】->【🔑 绑定 Token】，一键绑定你的 GitHub Personal Access Token (以 ghp_ 开头) 即可直接成功推送！";
                }

                MessageBox.Show($"推送失败，详情如下：\n\n{output}{tip}", "推送失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"推送发生异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PullAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath))
        {
            MessageBox.Show("请先打开一个 Git 仓库！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var remotes = _gitService.GetRemotes(RepoPath);
        if (remotes.Count == 0)
        {
            MessageBox.Show("当前仓库尚未配置远程源！\n\n请先点击【🌐 远程管理】添加远程仓库。", "未配置远程仓库", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        StatusMessage = $"正在从远程拉取分支 [{CurrentBranch}] 更新...";

        try
        {
            var (success, output) = await _gitService.PullBranchAsync(RepoPath);
            if (success)
            {
                StatusMessage = $"🎉 拉取完成！";
                MessageBox.Show($"拉取成功！\n\n输出详情:\n{output}", "拉取成功", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshRepositoryAsync();
            }
            else
            {
                StatusMessage = $"拉取遇到提示或失败: {output}";
                string tip = "";
                if (output.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
                {
                    tip = "\n\n⚠️ 检测到代码合并冲突！\n请点击顶部【⚡ 3-Way 冲突解决器】或查看未暂存文件进行冲突合并。";
                }
                MessageBox.Show($"拉取详情如下：\n\n{output}{tip}", "拉取结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                await RefreshRepositoryAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"拉取发生异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath))
        {
            MessageBox.Show("请先打开一个 Git 仓库！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var remotes = _gitService.GetRemotes(RepoPath);
        if (remotes.Count == 0)
        {
            MessageBox.Show("当前仓库尚未配置远程源！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsLoading = true;
        StatusMessage = "正在从远程抓取最新变更 (git fetch)...";

        try
        {
            var (success, output) = await _gitService.FetchAsync(RepoPath);
            if (success)
            {
                StatusMessage = "远程抓取完成，已刷新分支与同步状态！";
                await RefreshRepositoryAsync();
            }
            else
            {
                StatusMessage = $"抓取失败: {output}";
                MessageBox.Show($"抓取远程变更失败:\n\n{output}", "抓取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"抓取异常: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ManageRemotes()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath))
        {
            MessageBox.Show("请先打开一个 Git 仓库！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new ManageRemotesDialog(RepoPath, CurrentBranch, _gitService)
        {
            Owner = Application.Current.MainWindow
        };
        dlg.ShowDialog();
        if (dlg.HasChanges)
        {
            _ = RefreshRepositoryAsync();
        }
    }

    [RelayCommand]
    public void CreateBranch()
    {
        if (string.IsNullOrWhiteSpace(RepoPath) || !_gitService.IsGitRepository(RepoPath))
        {
            MessageBox.Show("请先打开一个 Git 仓库！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new CreateBranchDialog(RepoPath, _gitService)
        {
            Owner = Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true && dlg.Created)
        {
            _ = RefreshRepositoryAsync();
        }
    }

    [RelayCommand]
    public void CheckoutBranch(GitBranchItem? branch)
    {
        if (branch == null || branch.IsCurrentHead || string.IsNullOrWhiteSpace(RepoPath)) return;
        try
        {
            _gitService.CheckoutBranch(RepoPath, branch.Name);
            StatusMessage = $"已切换到分支: {branch.FriendlyName}";
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"切换分支失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void DeleteBranch(GitBranchItem? branch)
    {
        if (branch == null || branch.IsCurrentHead || string.IsNullOrWhiteSpace(RepoPath)) return;

        var result = MessageBox.Show($"确定要删除分支 [{branch.FriendlyName}] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (branch.IsRemote)
            {
                string remoteBranch = branch.FriendlyName;
                int slashIndex = remoteBranch.IndexOf('/');
                if (slashIndex > 0)
                {
                    string remoteName = remoteBranch.Substring(0, slashIndex);
                    string branchName = remoteBranch.Substring(slashIndex + 1);
                    var (success, output) = _gitService.DeleteRemoteBranch(RepoPath, remoteName, branchName);
                    if (!success)
                    {
                        MessageBox.Show($"删除远程分支失败: {output}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }
            else
            {
                _gitService.DeleteBranch(RepoPath, branch.Name);
            }
            StatusMessage = $"已删除分支: {branch.FriendlyName}";
            _ = RefreshRepositoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除分支失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
