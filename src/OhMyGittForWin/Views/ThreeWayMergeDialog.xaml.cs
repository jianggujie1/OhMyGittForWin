using System.Windows;
using OhMyGittForWin.ViewModels;

namespace OhMyGittForWin.Views;

public partial class ThreeWayMergeDialog : Window
{
    public ThreeWayMergeViewModel ViewModel => (ThreeWayMergeViewModel)DataContext;

    public ThreeWayMergeDialog()
    {
        InitializeComponent();
    }

    public ThreeWayMergeDialog(string conflictFilePath) : this()
    {
        ViewModel.LoadConflictFile(conflictFilePath);
    }
}
