using RS.OCR.Core.Models;

namespace RS.OCR.Core.Engine;

public class TableStructureEngine
{
    public float RowTolerance { get; set; } = 15f;
    public float MinConfidence { get; set; } = 0.3f;

    public TableResult BuildTable(List<TextBlock> blocks)
    {
        var result = new TableResult();
        if (blocks == null || blocks.Count == 0) return result;

        var validBlocks = blocks.Where(b => b.Confidence >= MinConfidence).ToList();
        if (validBlocks.Count == 0) return result;

        var rows = GroupIntoRows(validBlocks);
        foreach (var row in rows)
        {
            var cells = row.OrderBy(b => b.BoxPoints[0].X).Select(b => new TableCell
            {
                Text = b.Text,
                Confidence = b.Confidence,
                X = b.BoxPoints[0].X,
                Y = b.BoxPoints[0].Y,
                Width = b.BoxPoints[1].X - b.BoxPoints[0].X,
                Height = b.BoxPoints[2].Y - b.BoxPoints[0].Y
            }).ToList();
            result.Rows.Add(new TableRow { Cells = cells });
        }
        NormalizeColumns(result);
        return result;
    }

    private List<List<TextBlock>> GroupIntoRows(List<TextBlock> blocks)
    {
        var sorted = blocks.OrderBy(b => b.BoxPoints[0].Y).ToList();
        var rows = new List<List<TextBlock>>();
        foreach (var block in sorted)
        {
            float blockY = block.BoxPoints[0].Y;
            bool assigned = false;
            foreach (var row in rows)
            {
                float rowY = row.Average(b => b.BoxPoints[0].Y);
                if (Math.Abs(blockY - rowY) <= RowTolerance)
                { row.Add(block); assigned = true; break; }
            }
            if (!assigned) rows.Add(new List<TextBlock> { block });
        }
        return rows;
    }

    private void NormalizeColumns(TableResult result)
    {
        int maxCols = result.Rows.Max(r => r.Cells.Count);
        foreach (var row in result.Rows)
            while (row.Cells.Count < maxCols) row.Cells.Add(new TableCell());
    }
}