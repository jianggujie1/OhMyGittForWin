using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OhMyGittForWin.Models;
using OhMyGittForWin.Services;

namespace OhMyGittForWin.Views;

public partial class ManageRemotesDialog : Window
{
    private readonly string _repoPath;
    private readonly string _currentBranch;
    private readonly IGitService _gitService;

    public bool HasChanges { get; private set; }

    public ManageRemotesDialog(string repoPath, string currentBranch, IGitService gitService)
    {
        InitializeComponent();
        _repoPath = repoPath;
        _currentBranch = currentBranch;
        _gitService = gitService;

        TxtLocalBranch.Text = _currentBranch;
        TxtTargetRemoteBranch.Text = _currentBranch;

        LoadRemotes();
    }

    private void LoadRemotes()
    {
        try
        {
            var remotes = _gitService.GetRemotes(_repoPath);
            RemotesListBox.ItemsSource = remotes;
            if (remotes.Count > 0)
            {
                RemotesListBox.SelectedIndex = 0;
            }
            else
            {
                TxtStatus.Text = "当前仓库尚未配置远程源。请输入名称和 URL 点击添加。";
                BtnFetch.IsEnabled = false;
                BtnPush.IsEnabled = false;
                BtnDeleteRemote.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"获取远程仓库失败: {ex.Message}";
        }
    }

    private void RemotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = RemotesListBox.SelectedItem is GitRemoteItem;
        BtnFetch.IsEnabled = hasSelection;
        BtnPush.IsEnabled = hasSelection;
        BtnDeleteRemote.IsEnabled = hasSelection;

        if (RemotesListBox.SelectedItem is GitRemoteItem selected)
        {
            TxtStatus.Text = $"已选中远程源: {selected.Name} ({selected.Url}) | 当前分支: {_currentBranch}";
        }
    }

    private void BtnAddRemote_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtRemoteName.Text?.Trim() ?? string.Empty;
        string url = TxtRemoteUrl.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("请输入远程源别名（如 origin）！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("请输入远程仓库 URL（如 https://github.com/用户名/仓库名.git）！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _gitService.AddRemote(_repoPath, name, url);
            TxtStatus.Text = $"成功添加远程源 [{name}]！";
            TxtRemoteUrl.Clear();
            HasChanges = true;
            LoadRemotes();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"添加远程源失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDeleteRemote_Click(object sender, RoutedEventArgs e)
    {
        if (RemotesListBox.SelectedItem is not GitRemoteItem selected) return;

        var result = MessageBox.Show($"确定要删除远程源 [{selected.Name}] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            _gitService.RemoveRemote(_repoPath, selected.Name);
            TxtStatus.Text = $"已删除远程源 [{selected.Name}]。";
            HasChanges = true;
            LoadRemotes();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除远程源失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnPush_Click(object sender, RoutedEventArgs e)
    {
        if (RemotesListBox.SelectedItem is not GitRemoteItem selected) return;

        string localBranch = TxtLocalBranch.Text?.Trim() ?? _currentBranch;
        string remoteBranch = TxtTargetRemoteBranch.Text?.Trim() ?? localBranch;

        if (string.IsNullOrWhiteSpace(localBranch))
        {
            MessageBox.Show("请输入要推送的本地分支名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(remoteBranch))
        {
            remoteBranch = localBranch;
        }

        string refspec = string.Equals(localBranch, remoteBranch, StringComparison.OrdinalIgnoreCase)
            ? localBranch
            : $"{localBranch}:{remoteBranch}";

        TxtStatus.Text = $"正在推送分支 [{localBranch}] 到远程源 [{selected.Name}/{remoteBranch}]...";
        BtnPush.IsEnabled = false;

        var (success, output) = await Task.Run(() => _gitService.PushBranch(_repoPath, selected.Name, refspec, true));

        if (success)
        {
            // Auto fetch to sync remote branch refs
            await Task.Run(() => _gitService.Fetch(_repoPath, selected.Name));
            BtnPush.IsEnabled = true;
            HasChanges = true;
            TxtStatus.Text = $"🎉 成功创建/推送到远程分支 {selected.Name}/{remoteBranch}！";
            MessageBox.Show($"成功推送到远程并创建远程跟踪分支！\n\n远程分支: {selected.Name}/{remoteBranch}\n\n输出详情:\n{output}", "推送成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            BtnPush.IsEnabled = true;
            TxtStatus.Text = $"推送失败: {output}";
            MessageBox.Show($"推送失败，详情如下：\n\n{output}", "推送失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnFetch_Click(object sender, RoutedEventArgs e)
    {
        if (RemotesListBox.SelectedItem is not GitRemoteItem selected) return;

        TxtStatus.Text = $"正在从 [{selected.Name}] 拉取远程分支...";
        BtnFetch.IsEnabled = false;

        var (success, output) = await Task.Run(() => _gitService.Fetch(_repoPath, selected.Name));

        BtnFetch.IsEnabled = true;
        HasChanges = true;

        if (success)
        {
            TxtStatus.Text = $"🎉 拉取完成，远程分支列表已更新！";
        }
        else
        {
            TxtStatus.Text = $"拉取失败: {output}";
            MessageBox.Show($"拉取失败: {output}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
