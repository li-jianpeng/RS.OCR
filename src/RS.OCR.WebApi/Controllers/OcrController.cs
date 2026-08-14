using Microsoft.AspNetCore.Mvc;
using RS.OCR.Core.Engine;
using RS.OCR.Core.Models;

namespace RS.OCR.WebApi.Controllers;

/// <summary>
/// OCR识别服务
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly OcrEngine _ocr;
    private readonly TableStructureEngine _tbl;
    private readonly ILogger<OcrController> _log;
    /// <summary>
    /// OCR识别服务
    /// </summary>
    /// <param name="ocr"></param>
    /// <param name="log"></param>
    public OcrController(OcrEngine ocr, ILogger<OcrController> log)
    {
        _ocr = ocr;
        _tbl = new TableStructureEngine();
        _log = log;
    }

    /// <summary>
    /// 通用文字识别V1
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpPost("RecognizeText")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> RecognizeText(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "请上传图片" });
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var result = _ocr.DetectFromBytes(ms.ToArray());
            return Ok(FormatOcrResult(result));
        }
        catch (Exception ex) { _log.LogError(ex, "OCR failed"); return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// 通用文字识别V1_Base64
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>

    [HttpPost("RecognizeTextBase64")]
    public IActionResult RecognizeTextBase64([FromBody] ImageBase64Request req)
    {
        if (string.IsNullOrEmpty(req?.ImageBase64))
            return BadRequest(new { error = "请提供 Base64 数据" });
        try
        {
            var result = _ocr.DetectFromBytes(Convert.FromBase64String(req.ImageBase64));
            return Ok(FormatOcrResult(result));
        }
        catch (Exception ex) { _log.LogError(ex, "OCR failed"); return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// 表格识别V1
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpPost("RecognizeTable")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> RecognizeTable(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "请上传图片" });
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var result = _ocr.DetectFromBytes(ms.ToArray());
            var table = _tbl.BuildTable(result.TextBlocks);
            table.ElapsedMs = result.ElapsedMs;
            return Ok(FormatTableResult(table));
        }
        catch (Exception ex) { _log.LogError(ex, "Table OCR failed"); return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// 表格识别V1_Base64
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>

    [HttpPost("RecognizeTableBase64")]
    public IActionResult RecognizeTableBase64([FromBody] ImageBase64Request req)
    {
        if (string.IsNullOrEmpty(req?.ImageBase64))
            return BadRequest(new { error = "请提供 Base64 数据" });
        try
        {
            var result = _ocr.DetectFromBytes(Convert.FromBase64String(req.ImageBase64));
            var table = _tbl.BuildTable(result.TextBlocks);
            table.ElapsedMs = result.ElapsedMs;
            return Ok(FormatTableResult(table));
        }
        catch (Exception ex) { _log.LogError(ex, "Table OCR failed"); return StatusCode(500, new { error = ex.Message }); }
    }

    [NonAction]
    private static object FormatOcrResult(OcrResult r) => new
    {
        success = true,
        text = r.Text,
        blocks = r.TextBlocks.Select(b => new
        {
            text = b.Text,
            confidence = Math.Round(b.Confidence, 4),
            box = b.BoxPoints.Select(p => new { x = p.X, y = p.Y })
        }),
        elapsedMs = r.ElapsedMs
    };

    [NonAction]
    private static object FormatTableResult(TableResult t) => new
    {
        success = true,
        rowCount = t.RowCount,
        colCount = t.ColCount,
        rows = t.Rows.Select(r => new { cells = r.Cells.Select(c => new { text = c.Text, confidence = Math.Round(c.Confidence, 4), x = c.X, y = c.Y, w = c.Width, h = c.Height }) }),
        elapsedMs = t.ElapsedMs
    };
}

/// <summary>
/// 公共参数
/// </summary>
public class ImageBase64Request { public string ImageBase64 { get; set; } = ""; }