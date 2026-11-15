using System.Drawing;
using System.Drawing.Imaging;

namespace LmStudioOcrTray;

internal static class ImageEncoder
{
    public static string ToPngDataUrl(Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}
