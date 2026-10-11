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

        int oldLine = 1;
        int newLine = 1;

        // Unified list representation
        int maxLines = System.Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count);
        for (int i = 0; i < maxLines; i++)
        {
            var oldPiece = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var newPiece = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            if (oldPiece != null && oldPiece.Type == ChangeType.Deleted)
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = oldLine++,
                    NewLineNumber = null,
                    Text = "- " + (oldPiece.Text ?? string.Empty),
                    Type = DiffLineType.Deleted
                });
                result.Deletions++;
            }
            else if (newPiece != null && newPiece.Type == ChangeType.Inserted)
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = null,
                    NewLineNumber = newLine++,
                    Text = "+ " + (newPiece.Text ?? string.Empty),
                    Type = DiffLineType.Inserted
                });
                result.Additions++;
            }
            else if (newPiece != null && newPiece.Type == ChangeType.Modified)
            {
                if (oldPiece != null && !string.IsNullOrEmpty(oldPiece.Text))
                {
                    result.Lines.Add(new DiffLineModel
                    {
                        OldLineNumber = oldLine++,
                        NewLineNumber = null,
                        Text = "- " + oldPiece.Text,
                        Type = DiffLineType.Deleted
                    });
                    result.Deletions++;
                }

                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = null,
                    NewLineNumber = newLine++,
                    Text = "+ " + (newPiece.Text ?? string.Empty),
                    Type = DiffLineType.Inserted
                });
                result.Additions++;
            }
            else
            {
                result.Lines.Add(new DiffLineModel
                {
                    OldLineNumber = oldPiece != null && oldPiece.Type != ChangeType.Imaginary ? oldLine++ : (int?)null,
                    NewLineNumber = newPiece != null && newPiece.Type != ChangeType.Imaginary ? newLine++ : (int?)null,
                    Text = "  " + (newPiece?.Text ?? oldPiece?.Text ?? string.Empty),
                    Type = DiffLineType.Unchanged
                });
            }
        }

        return result;
    }
}
