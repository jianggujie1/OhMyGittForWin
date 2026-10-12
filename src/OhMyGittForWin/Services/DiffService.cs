using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public IdeaFileDiffResult BuildIdeaDiff(string filePath, string oldText, string newText, string leftCommitSha = "HEAD", string rightTitle = "Current version")
    {
        var model = _diffBuilder.BuildDiffModel(oldText ?? string.Empty, newText ?? string.Empty);
        var result = new IdeaFileDiffResult
        {
            FilePath = filePath,
            LeftCommitSha = leftCommitSha,
            RightTitle = rightTitle,
            Hunks = new ObservableCollection<IdeaDiffHunk>()
        };

        int maxLines = Math.Max(model.OldText.Lines.Count, model.NewText.Lines.Count);
        if (maxLines == 0) return result;

        // Gap threshold for merging micro-fragments into a single cohesive hunk (standard Git diff context is <= 6 lines)
        const int GapThreshold = 8;

        int hunkId = 1;
        int i = 0;

        while (i < maxLines)
        {
            var op = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var np = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            bool isUnchanged = (op != null && op.Type == ChangeType.Unchanged) &&
                              (np != null && np.Type == ChangeType.Unchanged);

            if (isUnchanged)
            {
                // Measure the length of this contiguous unchanged gap
                int gapLength = 0;
                int peek = i;
                while (peek < maxLines)
                {
                    var pop = peek < model.OldText.Lines.Count ? model.OldText.Lines[peek] : null;
                    var pnp = peek < model.NewText.Lines.Count ? model.NewText.Lines[peek] : null;
                    if ((pop != null && pop.Type == ChangeType.Unchanged) &&
                        (pnp != null && pnp.Type == ChangeType.Unchanged))
                    {
                        gapLength++;
                        peek++;
                    }
                    else
                    {
                        break;
                    }
                }

                // If this is a small unchanged gap between two changes, absorb it into the changed block!
                bool isSmallGapBetweenChanges = (hunkId > 1) && (gapLength <= GapThreshold) && (peek < maxLines);

                if (!isSmallGapBetweenChanges)
                {
                    // Genuine unchanged block
                    var unchangedHunk = new IdeaDiffHunk
                    {
                        HunkId = hunkId++,
                        Type = ChangeType.Unchanged
                    };

                    for (int g = 0; g < gapLength; g++)
                    {
                        var curOp = (i + g) < model.OldText.Lines.Count ? model.OldText.Lines[i + g] : null;
                        var curNp = (i + g) < model.NewText.Lines.Count ? model.NewText.Lines[i + g] : null;

                        var leftLine = new IdeaDiffLine
                        {
                            LineNumber = curOp?.Position,
                            Text = curOp?.Text ?? string.Empty,
                            Type = ChangeType.Unchanged
                        };
                        leftLine.WordPieces.Add(new IdeaWordPiece { Text = leftLine.Text, IsHighlighted = false });
                        unchangedHunk.LeftLines.Add(leftLine);

                        var rightLine = new IdeaDiffLine
                        {
                            LineNumber = curNp?.Position,
                            Text = curNp?.Text ?? string.Empty,
                            Type = ChangeType.Unchanged
                        };
                        rightLine.WordPieces.Add(new IdeaWordPiece { Text = rightLine.Text, IsHighlighted = false });
                        unchangedHunk.RightLines.Add(rightLine);
                    }

                    i += gapLength;
                    result.Hunks.Add(unchangedHunk);
                    continue;
                }
            }

            // Changed block (collect contiguous diff lines, absorbing small unchanged gaps)
            var rawLeftLines = new List<IdeaDiffLine>();
            var rawRightLines = new List<IdeaDiffLine>();

            while (i < maxLines)
            {
                var curOp = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
                var curNp = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

                bool curUnchanged = (curOp != null && curOp.Type == ChangeType.Unchanged) &&
                                    (curNp != null && curNp.Type == ChangeType.Unchanged);

                if (curUnchanged)
                {
                    // Peek ahead to see if the unchanged block exceeds GapThreshold
                    int gap = 0;
                    int peek = i;
                    while (peek < maxLines)
                    {
                        var pop = peek < model.OldText.Lines.Count ? model.OldText.Lines[peek] : null;
                        var pnp = peek < model.NewText.Lines.Count ? model.NewText.Lines[peek] : null;
                        if ((pop != null && pop.Type == ChangeType.Unchanged) &&
                            (pnp != null && pnp.Type == ChangeType.Unchanged))
                        {
                            gap++;
                            peek++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (gap > GapThreshold || peek >= maxLines)
                    {
                        // Genuine large unchanged region reached: finish this changed hunk!
                        break;
                    }
                }

                if (curOp != null && curOp.Type != ChangeType.Imaginary)
                {
                    var leftLine = new IdeaDiffLine
                    {
                        LineNumber = curOp.Position,
                        Text = curOp.Text ?? string.Empty,
                        Type = curOp.Type
                    };

                    if (curOp.SubPieces != null && curOp.SubPieces.Count > 0)
                    {
                        foreach (var sp in curOp.SubPieces)
                        {
                            leftLine.WordPieces.Add(new IdeaWordPiece
                            {
                                Text = sp.Text ?? string.Empty,
                                IsHighlighted = sp.Type != ChangeType.Unchanged
                            });
                        }
                    }
                    else
                    {
                        leftLine.WordPieces.Add(new IdeaWordPiece { Text = leftLine.Text, IsHighlighted = false });
                    }

                    rawLeftLines.Add(leftLine);
                }

                if (curNp != null && curNp.Type != ChangeType.Imaginary)
                {
                    var rightLine = new IdeaDiffLine
                    {
                        LineNumber = curNp.Position,
                        Text = curNp.Text ?? string.Empty,
                        Type = curNp.Type
                    };

                    if (curNp.SubPieces != null && curNp.SubPieces.Count > 0)
                    {
                        foreach (var sp in curNp.SubPieces)
                        {
                            rightLine.WordPieces.Add(new IdeaWordPiece
                            {
                                Text = sp.Text ?? string.Empty,
                                IsHighlighted = sp.Type != ChangeType.Unchanged
                            });
                        }
                    }
                    else
                    {
                        rightLine.WordPieces.Add(new IdeaWordPiece { Text = rightLine.Text, IsHighlighted = false });
                    }

                    rawRightLines.Add(rightLine);
                }

                i++;
            }

            // Determine overall hunk type
            ChangeType overallType;
            if (rawLeftLines.Count == 0 && rawRightLines.Count > 0)
            {
                overallType = ChangeType.Inserted;
                result.Additions += rawRightLines.Count;
            }
            else if (rawLeftLines.Count > 0 && rawRightLines.Count == 0)
            {
                overallType = ChangeType.Deleted;
                result.Deletions += rawLeftLines.Count;
            }
            else
            {
                overallType = ChangeType.Modified;
                result.Modifications += Math.Max(rawLeftLines.Count, rawRightLines.Count);
            }

            // Unify every line's color to the hunk's overall type
            foreach (var line in rawLeftLines) line.Type = overallType;
            foreach (var line in rawRightLines) line.Type = overallType;

            var changedHunk = new IdeaDiffHunk
            {
                HunkId = hunkId++,
                Type = overallType,
                LeftLines = new ObservableCollection<IdeaDiffLine>(rawLeftLines),
                RightLines = new ObservableCollection<IdeaDiffLine>(rawRightLines)
            };

            result.TotalDifferences++;
            result.Hunks.Add(changedHunk);
        }

        return result;
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

            if (newType == ChangeType.Inserted) result.Additions++;
            else if (oldType == ChangeType.Deleted) result.Deletions++;
            else if (newType == ChangeType.Modified || oldType == ChangeType.Modified) result.Modifications++;
        }

        return result;
    }
}
