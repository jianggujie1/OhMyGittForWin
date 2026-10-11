using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OhMyGittForWin.Models;
using OhMyGittForWin.Services;

namespace OhMyGittForWin.ViewModels;

public partial class ThreeWayMergeViewModel : ObservableObject
{
    private readonly MergeService _mergeService = new();

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private ConflictFileModel? _conflictModel;

    [ObservableProperty]
    private MergeConflictChunk? _selectedChunk;

    [ObservableProperty]
    private string _statusText = "请处理所有冲突块后点击【保存并解决】";

    public ObservableCollection<MergeConflictChunk> Chunks { get; } = new();

    public void LoadConflictFile(string fullPath)
    {
        FilePath = fullPath;
        FileName = Path.GetFileName(fullPath);

        ConflictModel = _mergeService.ParseConflictFile(fullPath);
        Chunks.Clear();
        foreach (var c in ConflictModel.Chunks)
        {
            Chunks.Add(c);
        }

        if (Chunks.Count > 0)
        {
            SelectedChunk = Chunks[0];
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int unresolved = 0;
        foreach (var c in Chunks)
        {
            if (c.IsConflict && c.State == ConflictResolutionState.Unresolved)
            {
                unresolved++;
            }
        }

        if (unresolved == 0)
        {
            StatusText = "✅ 所有冲突已解决！可以点击【保存并解决】完成合并。";
        }
        else
        {
            StatusText = $"⚠️ 尚有 {unresolved} 处冲突未解决。";
        }
    }

    [RelayCommand]
    public void AcceptLocal(MergeConflictChunk? chunk)
    {
        chunk ??= SelectedChunk;
        if (chunk == null) return;

        chunk.ResultText = chunk.LocalText;
        chunk.State = ConflictResolutionState.AcceptedLocal;
        UpdateStatus();
    }

    [RelayCommand]
    public void AcceptRemote(MergeConflictChunk? chunk)
    {
        chunk ??= SelectedChunk;
        if (chunk == null) return;

        chunk.ResultText = chunk.RemoteText;
        chunk.State = ConflictResolutionState.AcceptedRemote;
        UpdateStatus();
    }

    [RelayCommand]
    public void AcceptBoth(MergeConflictChunk? chunk)
    {
        chunk ??= SelectedChunk;
        if (chunk == null) return;

        chunk.ResultText = chunk.LocalText + chunk.RemoteText;
        chunk.State = ConflictResolutionState.AcceptedBoth;
        UpdateStatus();
    }

    [RelayCommand]
    public void AutoResolveAll()
    {
        if (ConflictModel == null) return;
        _mergeService.AutoResolveSimpleConflicts(ConflictModel);
        UpdateStatus();
    }

    [RelayCommand]
    public void SaveAndClose(Window window)
    {
        if (ConflictModel == null) return;

        try
        {
            _mergeService.SaveResolvedFile(ConflictModel);
            MessageBox.Show("冲突已成功合并并保存！", "合并成功", MessageBoxButton.OK, MessageBoxImage.Information);
            window.DialogResult = true;
            window.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
