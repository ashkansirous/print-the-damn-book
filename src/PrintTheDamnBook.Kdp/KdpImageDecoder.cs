using PrintTheDamnBook.Core.Kdp;
using PrintTheDamnBook.Kdp.Pictures;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp;

/// <summary>A picture ready for its page: the JPEG at exactly the KDP pixel size, and what was done to it.</summary>
public sealed record KdpPicture(byte[] Jpeg, int SourceWidth, int SourceHeight, KdpCrop Crop, CutLineScore Cut, bool OffsetChosen, byte[]? Preview, int RequestedOffsetPx = 0, int MaxOffsetPx = 0)
{
    /// <summary>The offset actually used (the request, clamped so the kept part stays inside the picture).</summary>
    public int AppliedOffsetPx => Crop.Source.Y;

    /// <summary>True when the owner chose an offset that moved nothing (a picture with no spare height, or a request of 0 from a hand-edited file is not counted).</summary>
    public bool OffsetIgnored => OffsetChosen && RequestedOffsetPx > 0 && MaxOffsetPx == 0;

    /// <summary>True when the owner asked for more than the picture allows and got the largest possible.</summary>
    public bool OffsetClamped => OffsetChosen && MaxOffsetPx > 0 && RequestedOffsetPx > MaxOffsetPx;

    /// <summary>True when a character would likely be cut and the owner has not chosen an offset for this page.</summary>
    public bool Flagged => !OffsetChosen && Cut.Flagged;
}

/// <summary>The one place a picture is decoded for KDP: reads it, applies its EXIF orientation, crops to the page and resamples to the exact pixel size.</summary>
public static class KdpImageDecoder
{
    public const int JpegQuality = 95;

    /// <summary>A picture is untrusted input: one with more pixels than this (width x height, 50 million, about 200 MB decoded) is refused before it is decoded.</summary>
    public const long MaxPixels = 50_000_000;

    /// <param name="offsetPx">How far down the kept part is moved (the owner's choice; clamped to the picture).</param>
    /// <param name="offsetChosen">True when the offset is the owner's own choice: the page is then never flagged over it.</param>
    public static KdpPicture Prepare(byte[] encoded, int offsetPx = 0, bool offsetChosen = false)
    {
        using var source = Decode(encoded);
        var crop = KdpPictureCrop.For(source.Width, source.Height, offsetPx);
        var cut = offsetChosen ? CutLineScore.None : CutLineCheck.Evaluate(CutLineInputs.Luma(source), crop.Source);
        var preview = cut.Flagged ? CutLineInputs.Preview(source, crop.Source) : null;
        using var page = Resample(source, crop);
        return new KdpPicture(Encode(page), source.Width, source.Height, crop, cut, offsetChosen, preview, offsetPx, KdpPictureCrop.MaxOffset(source.Width, source.Height));
    }

    private static SKBitmap Decode(byte[] encoded)
    {
        using var stream = new SKMemoryStream(encoded);
        using var codec = SKCodec.Create(stream) ?? throw new KdpException("A picture could not be read as an image.");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new KdpException($"A picture is {codec.Info.Width} x {codec.Info.Height} px, more than the {MaxPixels:N0} pixel limit.");
        }

        using var raw = SKBitmap.Decode(codec) ?? throw new KdpException("A picture could not be decoded.");
        return Orient(raw, codec.EncodedOrigin);
    }

    /// <summary>The picture upright: the EXIF orientation applied, drawn on white so a transparent PNG does not print black.</summary>
    private static SKBitmap Orient(SKBitmap raw, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var upright = new SKBitmap(swap ? raw.Height : raw.Width, swap ? raw.Width : raw.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(upright);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(OrientationMatrix(origin, raw.Width, raw.Height));
        using var image = SKImage.FromBitmap(raw);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        return upright;
    }

    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    private static SKBitmap Resample(SKBitmap source, KdpCrop crop)
    {
        var page = new SKBitmap(crop.TargetWidth, crop.TargetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(page);
        using var image = SKImage.FromBitmap(source);
        var from = new SKRect(crop.Source.X, crop.Source.Y, crop.Source.X + crop.Source.Width, crop.Source.Y + crop.Source.Height);
        var to = new SKRect(0, 0, crop.TargetWidth, crop.TargetHeight);
        canvas.DrawImage(image, from, to, SamplingFor(crop), null);
        return page;
    }

    /// <summary>Shrinking uses a mipmapped linear filter (a bare cubic filter aliases a picture much wider than the page); enlarging uses the Catmull-Rom cubic (sharper than Mitchell by 1.2 to 1.5 dB on a down-and-up round trip of two real pictures, no ringing seen side by side; Slice 28).</summary>
    private static SKSamplingOptions SamplingFor(KdpCrop crop)
    {
        return crop.Source.Width > crop.TargetWidth
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.CatmullRom);
    }

    private static byte[] Encode(SKBitmap page)
    {
        using var image = SKImage.FromBitmap(page);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality) ?? throw new KdpException("A picture could not be encoded.");
        return data.ToArray();
    }
}
