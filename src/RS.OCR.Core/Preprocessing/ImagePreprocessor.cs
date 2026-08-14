using SkiaSharp;

namespace RS.OCR.Core.Preprocessing;

public static class ImagePreprocessor
{
    public static SKBitmap LoadFromFile(string p) { using var s = File.OpenRead(p); return SKBitmap.Decode(s) ?? throw new Exception("load fail"); }
    public static SKBitmap LoadFromBytes(byte[] d) { return SKBitmap.Decode(d) ?? throw new Exception("decode fail"); }

    public static (SKBitmap, float, float) ResizeImage(SKBitmap src, int limit)
    {
        int w = src.Width, h = src.Height; float r = 1;
        if (Math.Max(w, h) > limit) { r = (float)limit / Math.Max(w, h); w = (int)(w * r); h = (int)(h * r); }
        int nw = ((w + 31) / 32) * 32, nh = ((h + 31) / 32) * 32;
        using var rs = src.Resize(new SKSizeI(w, h), SKFilterQuality.Medium) ?? throw new Exception("resize fail");
        var pad = new SKBitmap(nw, nh);
        using var cv = new SKCanvas(pad); cv.Clear(SKColors.White); cv.DrawBitmap(rs, 0, 0);
        return (pad, r, r);
    }
}
