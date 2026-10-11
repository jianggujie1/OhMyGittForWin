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

        int hunkId = 1;
        int i = 0;

        while (i < maxLines)
        {
            var oldPiece = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
            var newPiece = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

            bool isUnchanged = (oldPiece != null && oldPiece.Type == ChangeType.Unchanged) &&
                              (newPiece != null && newPiece.Type == ChangeType.Unchanged);

            if (isUnchanged)
            {
                // Unchanged block
                var hunk = new IdeaDiffHunk
                {
                    HunkId = hunkId++,
                    Type = ChangeType.Unchanged
                };

                while (i < maxLines)
                {
                    var op = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
                    var np = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

                    bool stillUnchanged = (op != null && op.Type == ChangeType.Unchanged) &&
                                          (np != null && np.Type == ChangeType.Unchanged);
                    if (!stillUnchanged) break;

                    hunk.LeftLines.Add(new IdeaDiffLine
                    {
                        LineNumber = op?.Position,
                        Text = op?.Text ?? string.Empty,
                        Type = ChangeType.Unchanged
                    });

                    hunk.RightLines.Add(new IdeaDiffLine
                    {
                        LineNumber = np?.Position,
                        Text = np?.Text ?? string.Empty,
                        Type = ChangeType.Unchanged
                    });

                    i++;
                }

                result.Hunks.Add(hunk);
            }
            else
            {
                // Changed block (collect contiguous diff lines)
                var rawLeftLines = new List<IdeaDiffLine>();
                var rawRightLines = new List<IdeaDiffLine>();

                while (i < maxLines)
                {
                    var op = i < model.OldText.Lines.Count ? model.OldText.Lines[i] : null;
                    var np = i < model.NewText.Lines.Count ? model.NewText.Lines[i] : null;

                    bool currentUnchanged = (op != null && op.Type == ChangeType.Unchanged) &&
                                            (np != null && np.Type == ChangeType.Unchanged);

                    // If we encounter a tiny 1-line unchanged gap (e.g. single closing brace '}'), peek ahead:
                    // If more changes immediately follow, absorb it so the change isn't fragmented!
                    if (currentUnchanged)
                    {
                        int lookAhead = i + 1;
                        bool followedByChange = false;
                        if (lookAhead < maxLines)
                        {
                            var nextOp = lookAhead < model.OldText.Lines.Count ? model.OldText.Lines[lookAhead] : null;
                            var nextNp = lookAhead < model.NewText.Lines.Count ? model.NewText.Lines[lookAhead] : null;
                            followedByChange = (nextOp != null && nextOp.Type != ChangeType.Unchanged) ||
                                               (nextNp != null && nextNp.Type != ChangeType.Unchanged);
                        }

                        if (!followedByChange)
                        {
                            break; // Genuine unchanged section begins
                        }
                    }

                    if (op != null && op.Type != ChangeType.Imaginary)
                    {
                        var leftLine = new IdeaDiffLine
                        {
                            LineNumber = op.Position,
                            Text = op.Text ?? string.Empty,
                            Type = op.Type
                        };

                        if (op.SubPieces != null && op.SubPieces.Count > 0)
                        {
                            foreach (var sp in op.SubPieces)
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

                    if (np != null && np.Type != ChangeType.Imaginary)
                    {
                        var rightLine = new IdeaDiffLine
                        {
                            LineNumber = np.Position,
                            Text = np.Text ?? string.Empty,
                            Type = np.Type
                        };

                        if (np.SubPieces != null && np.SubPieces.Count > 0)
                        {
                            foreach (var sp in np.SubPieces)
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

                // Determine CLEAR, UNAMBIGUOUS hunk type
                ChangeType overallType;
                if (rawLeftLines.Count == 0 && rawRightLines.Count > 0)
                {
                    overallType = ChangeType.Inserted; // Pure addition (+): All Green
                    result.Additions += rawRightLines.Count;
                }
                else if (rawLeftLines.Count > 0 && rawRightLines.Count == 0)
                {
                    overallType = ChangeType.Deleted; // Pure deletion (-): All Red
                    result.Deletions += rawLeftLines.Count;
                }
                else
                {
                    overallType = ChangeType.Modified; // Old replaced by New: All Blue!
                    result.Modifications += Math.Max(rawLeftLines.Count, rawRightLines.Count);
                }

                // UNIFY every line's color to the hunk's overall type!
                // This eliminates the confusing "zebra" alternating blue and green lines!
                foreach (var line in rawLeftLines) line.Type = overallType;
                foreach (var line in rawRightLines) line.Type = overallType;

                var hunk = new IdeaDiffHunk
                {
                    HunkId = hunkId++,
                    Type = overallType,
                    LeftLines = new ObservableCollection<IdeaDiffLine>(rawLeftLines),
                    RightLines = new ObservableCollection<IdeaDiffLine>(rawRightLines)
                };

                result.TotalDifferences++;
                result.Hunks.Add(hunk);
            }
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
