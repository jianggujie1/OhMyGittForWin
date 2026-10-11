using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OhMyGittForWin.Models;

public enum ConflictResolutionState
{
    Unresolved,
    AcceptedLocal,
    AcceptedRemote,
    AcceptedBoth,
    ManuallyResolved
}

public partial class MergeConflictChunk : ObservableObject
{
    public int ChunkIndex { get; set; }
    public string BaseText { get; set; } = string.Empty;
    public string LocalText { get; set; } = string.Empty;
    public string RemoteText { get; set; } = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private ConflictResolutionState _state = ConflictResolutionState.Unresolved;

    public bool IsConflict => LocalText != RemoteText;

    public string StatusDescription => State switch
    {
        ConflictResolutionState.AcceptedLocal => "已采纳左侧 (Local)",
        ConflictResolutionState.AcceptedRemote => "已采纳右侧 (Remote)",
        ConflictResolutionState.AcceptedBoth => "已合并双侧",
        ConflictResolutionState.ManuallyResolved => "已手动修改",
        _ => "⚠️ 存在冲突未解决"
    };
}

public class ConflictFileModel
{
    public required string FilePath { get; set; }
    public string BaseContent { get; set; } = string.Empty;
    public string LocalContent { get; set; } = string.Empty;
    public string RemoteContent { get; set; } = string.Empty;
    public List<MergeConflictChunk> Chunks { get; set; } = new();
    public bool AllResolved => Chunks.TrueForAll(c => !c.IsConflict || c.State != ConflictResolutionState.Unresolved);
}
