using System;
using System.Collections.Generic;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using OhMyGittForWin.Models;

namespace OhMyGittForWin.Services;

public class DiffService
{
    private readonly ISideBySideDiffBuilder _diffBuilder;

    public DiffService()
    {
        _diffBuilder = new SideBySideDiffBuilder(new Differ());
    }

    public FileDiffResult BuildDiff(string filePath, string oldText, string newText)
    {
        var model = _diffBuilder.BuildDiffModel(oldText ?? string.Empty, newText ?? string.Empty);
        var result = new FileDiffResult
        {
            FilePath = filePath,
            OldText = oldText ?? string.Empty,
            NewText = newText ?? string.Empty
        };

        int maxLines = Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count);

        for (int i = 0; i < maxLines; i++)
        {
            var oldPiece = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var newPiece = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            var oldType = oldPiece?.Type ?? ChangeType.Imaginary;
            var newType = newPiece?.Type ?? ChangeType.Imaginary;

            // Build Side-by-Side row (IDEA Style)
            var sbsRow = new SideBySideDiffRow
            {
                RowIndex = i,
                OldLineNumber = oldPiece?.Position,
                OldText = oldPiece?.Text ?? string.Empty,
                OldType = oldType,
                NewLineNumber = newPiece?.Position,
                NewText = newPiece?.Text ?? string.Empty,
                NewType = newType
            };
            result.SideBySideRows.Add(sbsRow);

            // Compute statistics
            if (newType == ChangeType.Inserted) result.Additions++;
            else if (oldType == ChangeType.Deleted) result.Deletions++;
            else if (newType == ChangeType.Modified || oldType == ChangeType.Modified) result.Modifications++;

            // Also keep unified list for fallback
            if (oldPiece != null && oldPiece.Type == ChangeType.Deleted)
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = oldPiece.Position,
                    NewLineNumber = null,
                    Text = "- " + (oldPiece.Text ?? string.Empty),
                    Type = DiffLineType.Deleted
                });
            }
            else if (newPiece != null && newPiece.Type == ChangeType.Inserted)
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = null,
                    NewLineNumber = newPiece.Position,
                    Text = "+ " + (newPiece.Text ?? string.Empty),
                    Type = DiffLineType.Inserted
                });
            }
            else if (newPiece != null && newPiece.Type == ChangeType.Modified)
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = oldPiece?.Position,
                    NewLineNumber = newPiece.Position,
                    Text = "~ " + (newPiece.Text ?? string.Empty),
                    Type = DiffLineType.Modified
                });
            }
            else
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = oldPiece?.Position,
                    NewLineNumber = newPiece?.Position,
                    Text = "  " + (newPiece?.Text ?? oldPiece?.Text ?? string.Empty),
                    Type = DiffLineType.Unchanged
                });
            }
        }

        return result;
    }
}
