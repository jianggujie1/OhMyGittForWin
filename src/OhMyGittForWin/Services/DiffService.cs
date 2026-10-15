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

        double maxLineWidth = 0;

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
                    const int ContextLines = 3;
                    int leadingCount = i > 0 ? ContextLines : 0;
                    int trailingCount = peek < maxLines ? ContextLines : 0;
                    int middleCount = gapLength - leadingCount - trailingCount;

                    if (middleCount >= 4)
                    {
                        if (leadingCount > 0)
                        {
                            result.Hunks.Add(CreateUnchangedHunk(ref hunkId, model, i, leadingCount, false, ref maxLineWidth));
                        }
                        result.Hunks.Add(CreateUnchangedHunk(ref hunkId, model, i + leadingCount, middleCount, true, ref maxLineWidth));
                        if (trailingCount > 0)
                        {
                            result.Hunks.Add(CreateUnchangedHunk(ref hunkId, model, i + leadingCount + middleCount, trailingCount, false, ref maxLineWidth));
                        }
                    }
                    else
                    {
                        result.Hunks.Add(CreateUnchangedHunk(ref hunkId, model, i, gapLength, false, ref maxLineWidth));
                    }

                    i += gapLength;
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

                    double wLeft = CalculateVisualLineWidth(leftLine.Text);
                    if (wLeft > maxLineWidth) maxLineWidth = wLeft;

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

                    double wRight = CalculateVisualLineWidth(rightLine.Text);
                    if (wRight > maxLineWidth) maxLineWidth = wRight;

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

        result.MaxLineWidth = maxLineWidth;
        return result;
    }

    private static double CalculateVisualLineWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        double width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\t')
            {
                width += 48.0; // 4-space tab in 12pt monospaced font
            }
            else if (c >= 0x4E00 && c <= 0x9FFF || c >= 0x3000 && c <= 0x303F || c >= 0xFF00 && c <= 0xFFEF)
            {
                width += 13.0; // Full-width CJK character
            }
            else
            {
                width += 7.2; // Monospace ASCII character
            }
        }
        return width;
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

    private IdeaDiffHunk CreateUnchangedHunk(ref int hunkId, SideBySideDiffModel model, int startIndex, int count, bool isFolded, ref double maxLineWidth)
    {
        var hunk = new IdeaDiffHunk
        {
            HunkId = hunkId++,
            Type = ChangeType.Unchanged,
            IsFolded = isFolded
        };

        for (int offset = 0; offset < count; offset++)
        {
            int idx = startIndex + offset;
            var curOp = idx < model.OldText.Lines.Count ? model.OldText.Lines[idx] : null;
            var curNp = idx < model.NewText.Lines.Count ? model.NewText.Lines[idx] : null;

            var leftText = curOp?.Text ?? string.Empty;
            var rightText = curNp?.Text ?? string.Empty;

            double wLeft = CalculateVisualLineWidth(leftText);
            if (wLeft > maxLineWidth) maxLineWidth = wLeft;
            double wRight = CalculateVisualLineWidth(rightText);
            if (wRight > maxLineWidth) maxLineWidth = wRight;

            hunk.LeftLines.Add(new IdeaDiffLine
            {
                LineNumber = curOp?.Position,
                Text = leftText,
                Type = ChangeType.Unchanged
            });

            hunk.RightLines.Add(new IdeaDiffLine
            {
                LineNumber = curNp?.Position,
                Text = rightText,
                Type = ChangeType.Unchanged
            });
        }

        return hunk;
    }
}
