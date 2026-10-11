using System.Windows;
using Microsoft.Win32;
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
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto open current directory if it is a git repo
        string currentDir = System.Environment.CurrentDirectory;
        if (ViewModel.OpenRepositoryCommand.CanExecute(currentDir))
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
        LogContentPanel.Visibility = Visibility.Visible;
        CommitContentPanel.Visibility = Visibility.Collapsed;
        ViewModel.SelectedTabIndex = 0;
    }

    private void TabCommit_Click(object sender, RoutedEventArgs e)
    {
        LogContentPanel.Visibility = Visibility.Collapsed;
        CommitContentPanel.Visibility = Visibility.Visible;
        ViewModel.SelectedTabIndex = 1;
    }
}