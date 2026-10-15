using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OhMyGittForWin.Models;
using OhMyGittForWin.ViewModels;
using OhMyGittForWin.Views;

namespace OhMyGittForWin;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto open current directory if it is a git repo
        string currentDir = System.Environment.CurrentDirectory;
        var gitService = new OhMyGittForWin.Services.GitService();
        if (!string.IsNullOrEmpty(gitService.DiscoverRepository(currentDir)))
        {
            await ViewModel.OpenRepositoryAsync(currentDir);
        }
    }

    private void OpenConflictResolver_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Title = "选择包含冲突标记的文件进行 3-Way 合并解决",
            Filter = "所有文件 (*.*)|*.*|代码文件 (*.cs;*.js;*.ts;*.java;*.py;*.txt;*.md)|*.cs;*.js;*.ts;*.java;*.py;*.txt;*.md"
        };

        if (openDialog.ShowDialog(this) == true)
        {
            var mergeDialog = new ThreeWayMergeDialog(openDialog.FileName)
            {
                Owner = this
            };
            mergeDialog.ShowDialog();
            _ = ViewModel.RefreshRepositoryAsync();
        }
    }

    private void TabLog_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(0);
    }

    private void TabCommit_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(1);
    }

    private void SwitchTab(int index)
    {
        var targetPanel = index == 0 ? LogContentPanel : CommitContentPanel;
        var hidePanel = index == 0 ? CommitContentPanel : LogContentPanel;

        if (targetPanel.Visibility == Visibility.Visible && ViewModel.SelectedTabIndex == index) return;

        hidePanel.Visibility = Visibility.Hidden;
        targetPanel.Visibility = Visibility.Visible;
        targetPanel.Opacity = 1.0;
        targetPanel.RenderTransform = null;
        ViewModel.SelectedTabIndex = index;
    }

    private void BranchesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is GitBranchItem branch)
        {
            if (!branch.IsCurrentHead)
            {
                ViewModel.CheckoutBranchCommand.Execute(branch);
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, System.EventArgs e)
    {
        UpdateMaximizeButton();
        if (WindowState == WindowState.Maximized)
        {
            RootContainer.Margin = new Thickness(7);
        }
        else
        {
            RootContainer.Margin = new Thickness(0);
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButton();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeButton()
    {
        if (BtnMaximize != null)
        {
            BtnMaximize.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }
    }
}