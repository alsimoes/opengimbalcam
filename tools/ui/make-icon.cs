// Generates src/OpenGimbalCam.Studio/Assets/app.ico (run from the repository root: dotnet run tools/ui/make-icon.cs).
// Glyph: Bootstrap Icons camera-video-fill (MIT), read from the generated Icons.axaml.
#:package SkiaSharp@2.88.9

using System.Text.RegularExpressions;
using SkiaSharp;

var xaml = File.ReadAllText("src/OpenGimbalCam.Studio/Styles/Icons.axaml");
var d = Regex.Match(xaml, """x:Key="IconCameraVideoFill">F\d (.+?)</StreamGeometry>""").Groups[1].Value;
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var images = sizes.Select(s => Render(s, d)).ToList();

using var ms = new MemoryStream();
using var w = new BinaryWriter(ms);
w.Write((ushort)0);
w.Write((ushort)1);
w.Write((ushort)sizes.Length);
int offset = 6 + 16 * sizes.Length;
for (int i = 0; i < sizes.Length; i++)
{
    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
    w.Write((byte)0);
    w.Write((byte)0);
    w.Write((ushort)1);
    w.Write((ushort)32);
    w.Write(images[i].Length);
    w.Write(offset);
    offset += images[i].Length;
}
foreach (var img in images)
    w.Write(img);
Directory.CreateDirectory("src/OpenGimbalCam.Studio/Assets");
File.WriteAllBytes("src/OpenGimbalCam.Studio/Assets/app.ico", ms.ToArray());
Console.WriteLine($"wrote app.ico ({ms.Length} bytes)");

static byte[] Render(int size, string pathData)
{
    using var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    float r = size * 0.22f;
    using (var paint = new SKPaint { IsAntialias = true })
    {
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(size, size),
            [SKColor.Parse("#7D6FF0"), SKColor.Parse("#4B3CC4")], null, SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, size, size), r, r), paint);
    }
    using var path = SKPath.ParseSvgPathData(pathData);
    path.FillType = SKPathFillType.EvenOdd;
    float scale = size * 0.62f / 16f;
    float off = (size - 16f * scale) / 2f;
    canvas.Translate(off, off);
    canvas.Scale(scale);
    using (var paint = new SKPaint { IsAntialias = true, Color = SKColors.White })
        canvas.DrawPath(path, paint);
    using var image = SKImage.FromBitmap(bmp);
    return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}
