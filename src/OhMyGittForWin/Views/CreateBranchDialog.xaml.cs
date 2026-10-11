using System;
using System.Windows;
using OhMyGittForWin.Services;

namespace OhMyGittForWin.Views;

public partial class CreateBranchDialog : Window
{
    private readonly string _repoPath;
    private readonly IGitService _gitService;

    public bool Created { get; private set; }
    public string NewBranchName { get; private set; } = string.Empty;

    public CreateBranchDialog(string repoPath, IGitService gitService)
    {
        InitializeComponent();
        _repoPath = repoPath;
        _gitService = gitService;
        Loaded += (s, e) => TxtBranchName.Focus();
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtBranchName.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("请输入分支名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _gitService.CreateBranch(_repoPath, name);
            if (ChkCheckout.IsChecked == true)
            {
                _gitService.CheckoutBranch(_repoPath, name);
            }
            Created = true;
            NewBranchName = name;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建分支失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
