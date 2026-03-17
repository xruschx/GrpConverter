using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GrpConverter;

public class SpriteSheetGenerator
{
    // Ile klatek w jednym wierszu
    private readonly int _columns;

    public SpriteSheetGenerator(int columns = 16)
    {
        _columns = columns;
    }

    public void Generate(List<Image<Rgba32>> frames, string outputPath, int frameWidth, int frameHeight)
    {
        if (frames.Count == 0) return;

        int columns = Math.Min(_columns, frames.Count);
        int rows    = (int)Math.Ceiling(frames.Count / (double)columns);

        int sheetWidth  = columns * frameWidth;
        int sheetHeight = rows    * frameHeight;

        Console.WriteLine($"Sprite sheet: {columns}×{rows} klatek, rozmiar {sheetWidth}×{sheetHeight}px");

        using var sheet = new Image<Rgba32>(sheetWidth, sheetHeight, Color.Transparent);

        for (int i = 0; i < frames.Count; i++)
        {
            int col = i % columns;
            int row = i / columns;

            int x = col * frameWidth;
            int y = row * frameHeight;

            // Narysuj klatkę na sprite sheecie
            sheet.Mutate(ctx => ctx.DrawImage(frames[i], new Point(x, y), 1f));
        }

        sheet.SaveAsPng(outputPath);
        Console.WriteLine($"Zapisano: {outputPath}");
    }
}