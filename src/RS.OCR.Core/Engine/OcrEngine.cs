using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RS.OCR.Core.Models;
using RS.OCR.Core.Preprocessing;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Diagnostics;

namespace RS.OCR.Core.Engine;

public class OcrEngine : IDisposable
{
    private readonly OcrConfig _cfg; private readonly ILogger<OcrEngine>? _log;
    private InferenceSession? _det, _rec; private List<string> _dict = new();
    private bool _init;

    public OcrEngine(OcrConfig cfg, ILogger<OcrEngine>? log = null) { _cfg = cfg; _log = log; }

    public void Initialize()
    {
        if (_init) return;
        _log?.LogInformation("Loading models...");
        var opts = new SessionOptions { InterOpNumThreads = _cfg.NumThread, IntraOpNumThreads = _cfg.NumThread,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL, EnableCpuMemArena = true };

        _det = new InferenceSession(_cfg.DetModelPath, opts);
        _rec = new InferenceSession(_cfg.RecModelPath, opts);
        _log?.LogInformation("Det in:{0} out:{1}  Rec in:{2} out:{3}",
            _det.InputNames[0], _det.OutputNames[0], _rec.InputNames[0], _rec.OutputNames[0]);

        if (File.Exists(_cfg.DictPath))
            _dict = File.ReadAllLines(_cfg.DictPath).Select(l => l.Trim()).ToList();
        if (_dict.Count == 0) for (char c = '0'; c <= '9'; c++) _dict.Add(c.ToString());

        _init = true;
    }

    public OcrResult DetectFromFile(string p) { using var b = ImagePreprocessor.LoadFromFile(p); return Detect(b); }
    public OcrResult DetectFromBytes(byte[] d) { using var b = ImagePreprocessor.LoadFromBytes(d); return Detect(b); }

    private OcrResult Detect(SKBitmap src)
    {
        if (!_init) throw new InvalidOperationException("Not init");
        var sw = Stopwatch.StartNew(); var r = new OcrResult();
        var boxes = RunDetection(src);
        if (boxes.Count == 0) { r.ElapsedMs = sw.ElapsedMilliseconds; return r; }
        var tbs = new TextBlock[boxes.Count];
        for (int i = 0; i < boxes.Count; i++) tbs[i] = RunRecognition(src, boxes[i]);
        r.TextBlocks = tbs.Where(t => t.Confidence >= _cfg.RecThresh)
            .OrderBy(t => t.BoxPoints[0].Y).ThenBy(t => t.BoxPoints[0].X).ToList();
        r.ElapsedMs = sw.ElapsedMilliseconds; return r;
    }

    private List<float[]> RunDetection(SKBitmap src)
    {
        var (pad, rw, rh) = ImagePreprocessor.ResizeImage(src, _cfg.DetLimitSideLen);
        using (pad) {
            var d = Norm(pad, out int w, out int h);
            using var o = _det!.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(_det.InputNames[0], new DenseTensor<float>(d, new[] { 1, 3, h, w })) });
            var detOut = o[0].AsTensor<float>();
            var dims = detOut.Dimensions.ToArray();
            int outLen = 1; foreach (var dim in dims) outLen *= (int)dim;
            if (outLen != w * h)
                throw new InvalidOperationException(
                    $"检测模型输出尺寸不匹配: 期望 {w}*{h}={w * h}, 实际 {outLen} (dims=[{string.Join(",", dims)}])");
            return PostDet(detOut.ToArray(), w, h, rw, rh);
        }
    }

    static float[] Norm(SKBitmap bmp, out int w, out int h) {
        w = bmp.Width; h = bmp.Height; int n = w * h; float[] d = new float[3 * n]; var px = bmp.Pixels;
        for (int i = 0; i < n; i++) { var p = px[i]; d[i] = p.Blue / 127.5f - 1; d[n + i] = p.Green / 127.5f - 1; d[2 * n + i] = p.Red / 127.5f - 1; }
        return d;
    }

    private List<float[]> PostDet(float[] m, int w, int h, float rw, float rh) {
        var bx = new List<float[]>(); byte[] mask = new byte[m.Length];
        for (int i = 0; i < m.Length; i++) if (m[i] > _cfg.DetThresh) mask[i] = 255;
        foreach (var c in FindCC(mask, w, h)) { var b = EB(c, w, h, rw, rh, m); if (b != null) { bx.Add(b); if (bx.Count >= _cfg.DetMaxCandidates) break; } }
        return bx;
    }

    private List<List<(int x, int y)>> FindCC(byte[] m, int w, int h) {
        var cs = new List<List<(int x, int y)>>(); var v = new bool[m.Length];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            if (m[y * w + x] == 0 || v[y * w + x]) continue;
            var c = new List<(int x, int y)>(); var q = new Queue<(int x, int y)>(); q.Enqueue((x, y)); v[y * w + x] = true;
            while (q.Count > 0) { var (cx, cy) = q.Dequeue(); c.Add((cx, cy));
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
                    if (dx == 0 && dy == 0) continue; int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue; int ni = ny * w + nx;
                    if (m[ni] == 0 || v[ni]) continue; v[ni] = true; q.Enqueue((nx, ny)); } }
            if (c.Count >= 10) cs.Add(c); }
        return cs;
    }

    private float[]? EB(List<(int x, int y)> r, int w, int h, float rw, float rh, float[] m) {
        int mnx = r.Min(p => p.x), mxx = r.Max(p => p.x), mny = r.Min(p => p.y), mxy = r.Max(p => p.y);
        if (mxx - mnx < 3 || mxy - mny < 3) return null; float s = 0;
        foreach (var (px, py) in r) s += m[py * w + px];
        if (s / r.Count < _cfg.DetBoxThresh) return null;
        return new[] { mnx / rw, mny / rh, mxx / rw, mny / rh, mxx / rw, mxy / rh, mnx / rw, mxy / rh };
    }

    private TextBlock RunRecognition(SKBitmap src, float[] box)
    {
        var bl = new TextBlock { Confidence = 0 };
        int pad = 10; int xmn = (int)Math.Max(0, Math.Min(Math.Min(box[0], box[6]), Math.Min(box[2], box[4])) - pad);
        int xmx = (int)Math.Min(src.Width, Math.Max(Math.Max(box[0], box[6]), Math.Max(box[2], box[4])) + pad);
        int ymn = (int)Math.Max(0, Math.Min(Math.Min(box[1], box[7]), Math.Min(box[3], box[5])) - pad);
        int ymx = (int)Math.Min(src.Height, Math.Max(Math.Max(box[1], box[7]), Math.Max(box[3], box[5])) + pad);
        if (xmx <= xmn || ymx <= ymn) return bl;
        int cw = xmx - xmn, ch = ymx - ymn;
        using var cr = new SKBitmap(cw, ch); src.ExtractSubset(cr, new SKRectI(xmn, ymn, xmx, ymx));
        int nw = Math.Max(1, (int)(cw * (float)_cfg.RecImageHeight / ch));
        using var rs = cr.Resize(new SKSizeI(nw, _cfg.RecImageHeight), SKFilterQuality.Medium);
        if (rs == null) return bl;
        var d = Norm(rs, out int w, out int h);
        using var o = _rec!.Run(new List<NamedOnnxValue> {
            NamedOnnxValue.CreateFromTensor(_rec.InputNames[0], new DenseTensor<float>(d, new[] { 1, 3, h, w })) });
        var logits = o[0].AsTensor<float>().ToArray();
        var dims = o[0].AsTensor<float>().Dimensions.ToArray();
        int sl = (int)dims[1], nc = (int)dims[2];

        string txt = Decode(logits, sl, nc, out float conf);
        bl.Text = txt; bl.Confidence = conf;
        bl.BoxPoints = new List<PointF> { new(box[0],box[1]),new(box[2],box[3]),new(box[4],box[5]),new(box[6],box[7]) };
        return bl;
    }

    private string Decode(float[] l, int sl, int nc, out float conf)
    {
        var chars = new List<int>(); var confs = new List<float>(); int p = -1;
        for (int t = 0; t < sl; t++) { int mx = 0; float mv = float.MinValue; int off = t * nc;
            for (int c = 0; c < nc; c++) { float v = l[off + c]; if (v > mv) { mv = v; mx = c; } }
            if (mx != p) { chars.Add(mx); confs.Add(mv); } p = mx; }
        var sb = new System.Text.StringBuilder(); var vc = new List<float>();
        for (int i = 0; i < chars.Count; i++)
            if (chars[i] > 0 && chars[i] <= _dict.Count) { sb.Append(_dict[chars[i] - 1]); vc.Add(confs[i]); }
        conf = vc.Count > 0 ? vc.Average() : 0; return sb.ToString();
    }

    public void Dispose() { _det?.Dispose(); _rec?.Dispose(); }
}