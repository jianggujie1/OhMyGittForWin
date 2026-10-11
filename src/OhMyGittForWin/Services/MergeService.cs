using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public class MergeService
{
    private const string ConflictMarkerStart = "<<<<<<<";
    private const string ConflictMarkerMid = "=======";
    private const string ConflictMarkerEnd = ">>>>>>>";

    /// <summary>
    /// Parses a file containing git conflict markers into distinct chunks
    /// </summary>
    public ConflictFileModel ParseConflictFile(string fullFilePath)
    {
        var model = new ConflictFileModel { FilePath = fullFilePath };
        if (!File.Exists(fullFilePath)) return model;

        var lines = File.ReadAllLines(fullFilePath);
        var chunks = new List<MergeConflictChunk>();

        int index = 0;
        bool inConflict = false;
        var nonConflictBuilder = new StringBuilder();
        var localBuilder = new StringBuilder();
        var remoteBuilder = new StringBuilder();
        bool inRemotePart = false;

        foreach (var line in lines)
        {
            if (line.StartsWith(ConflictMarkerStart))
            {
                if (nonConflictBuilder.Length > 0)
                {
                    chunks.Add(new MergeConflictChunk
                    {
                        ChunkIndex = index++,
                        LocalText = nonConflictBuilder.ToString(),
                        RemoteText = nonConflictBuilder.ToString(),
                        BaseText = nonConflictBuilder.ToString(),
                        ResultText = nonConflictBuilder.ToString(),
                        State = ConflictResolutionState.AcceptedBoth
                    });
                    nonConflictBuilder.Clear();
                }

                inConflict = true;
                inRemotePart = false;
                localBuilder.Clear();
                remoteBuilder.Clear();
            }
            else if (inConflict && line.StartsWith(ConflictMarkerMid))
            {
                inRemotePart = true;
            }
            else if (inConflict && line.StartsWith(ConflictMarkerEnd))
            {
                var chunk = new MergeConflictChunk
                {
                    ChunkIndex = index++,
                    LocalText = localBuilder.ToString(),
                    RemoteText = remoteBuilder.ToString(),
                    BaseText = string.Empty,
                    ResultText = string.Empty,
                    State = ConflictResolutionState.Unresolved
                };
                chunks.Add(chunk);

                inConflict = false;
                inRemotePart = false;
                localBuilder.Clear();
                remoteBuilder.Clear();
            }
            else
            {
                if (inConflict)
                {
                    if (inRemotePart)
                    {
                        remoteBuilder.AppendLine(line);
                    }
                    else
                    {
                        localBuilder.AppendLine(line);
                    }
                }
                else
                {
                    nonConflictBuilder.AppendLine(line);
                }
            }
        }

        if (nonConflictBuilder.Length > 0)
        {
            chunks.Add(new MergeConflictChunk
            {
                ChunkIndex = index++,
                LocalText = nonConflictBuilder.ToString(),
                RemoteText = nonConflictBuilder.ToString(),
                BaseText = nonConflictBuilder.ToString(),
                ResultText = nonConflictBuilder.ToString(),
                State = ConflictResolutionState.AcceptedBoth
            });
        }

        model.Chunks = chunks;
        return model;
    }

    /// <summary>
    /// Writes resolved chunks back to the file
    /// </summary>
    public void SaveResolvedFile(ConflictFileModel model)
    {
        var sb = new StringBuilder();
        foreach (var chunk in model.Chunks)
        {
            sb.Append(chunk.ResultText);
        }
        File.WriteAllText(model.FilePath, sb.ToString());
    }

    /// <summary>
    /// Auto resolves non-conflicting sections (Magic wand)
    /// </summary>
    public void AutoResolveSimpleConflicts(ConflictFileModel model)
    {
        foreach (var chunk in model.Chunks)
        {
            if (chunk.State == ConflictResolutionState.Unresolved)
            {
                if (string.IsNullOrWhiteSpace(chunk.LocalText) && !string.IsNullOrWhiteSpace(chunk.RemoteText))
                {
                    chunk.ResultText = chunk.RemoteText;
                    chunk.State = ConflictResolutionState.AcceptedRemote;
                }
                else if (!string.IsNullOrWhiteSpace(chunk.LocalText) && string.IsNullOrWhiteSpace(chunk.RemoteText))
                {
                    chunk.ResultText = chunk.LocalText;
                    chunk.State = ConflictResolutionState.AcceptedLocal;
                }
            }
        }
    }
}
