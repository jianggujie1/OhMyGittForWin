using System;
using System.Diagnostics;
using System.Windows;

namespace OhMyGittForWin.Views;

public partial class BindTokenDialog : Window
{
    public string Token { get; private set; } = string.Empty;

    public BindTokenDialog(string remoteName, string remoteUrl)
    {
        InitializeComponent();
        Loaded += (s, e) => TxtToken.Focus();
    }

    private void BtnOpenGitHubToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/settings/tokens/new?scopes=repo&description=OhMyGit",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开浏览器: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string token = TxtToken.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("请输入或粘贴 GitHub Token！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Token = token;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
