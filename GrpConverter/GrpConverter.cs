using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GrpConverter;

public class GrpConverter
{
    private readonly Color[] _palette;
    long frameTableBase = 6; // zaraz po nagłówku 6-bajtowym

    public GrpConverter(string palettePath)
    {
        _palette = LoadPalette(palettePath);
    }

    // Ładowanie palety z pliku .wpe
    private static Color[] LoadPalette(string path)
    {
        var palette = new Color[256];
        var data = File.ReadAllBytes(path);

        for (int i = 0; i < 256; i++)
        {
            int offset = i * 4;
            byte r = data[offset];
            byte g = data[offset + 1];
            byte b = data[offset + 2];
            // byte padding = data[offset + 3]; // ignorujemy
            palette[i] = Color.FromRgb(r, g, b);
        }

        return palette;
    }
    
    public void ConvertToSpriteSheet(string grpPath, string outputPath, int columns = 16)
    {
        using var stream = File.OpenRead(grpPath);
        using var reader = new BinaryReader(stream);

        ushort frameCount = reader.ReadUInt16();
        ushort maxWidth   = reader.ReadUInt16();
        ushort maxHeight  = reader.ReadUInt16();

        Console.WriteLine($"Frames: {frameCount}, Frame size: {maxWidth}×{maxHeight}");

        // Wczytaj tablicę klatek
        var frames = new FrameHeader[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = new FrameHeader
            {
                XOffset    = reader.ReadByte(),
                YOffset    = reader.ReadByte(),
                Width      = reader.ReadByte(),
                Height     = reader.ReadByte(),
                DataOffset = reader.ReadUInt32()
            };
        }

        // Dekoduj wszystkie klatki
        var decodedFrames = new List<Image<Rgba32>>();
        for (int i = 0; i < frameCount; i++)
        {
            var frame = DecodeFrame(reader, frames[i], maxWidth, maxHeight);
            decodedFrames.Add(frame);

            if (i % 20 == 0)
                Console.WriteLine($"Dekodowanie klatek: {i + 1}/{frameCount}");
        }

        // Wygeneruj sprite sheet
        var generator = new SpriteSheetGenerator(columns);
        generator.Generate(decodedFrames, outputPath, maxWidth, maxHeight);

        // Zwolnij pamięć
        foreach (var frame in decodedFrames)
            frame.Dispose();
    }

    // Główna metoda konwersji
    public void Convert(string grpPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        using var stream = File.OpenRead(grpPath);
        using var reader = new BinaryReader(stream);

        // Nagłówek
        ushort frameCount = reader.ReadUInt16();
        ushort maxWidth = reader.ReadUInt16();
        ushort maxHeight = reader.ReadUInt16();

        Console.WriteLine($"Frames: {frameCount}, Size: {maxWidth}x{maxHeight}");

        // Tablica klatek
        var frames = new FrameHeader[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = new FrameHeader
            {
                XOffset = reader.ReadByte(),
                YOffset = reader.ReadByte(),
                Width = reader.ReadByte(),
                Height = reader.ReadByte(),
                DataOffset = reader.ReadUInt32()
            };
        }

        // Eksport każdej klatki
        string baseName = Path.GetFileNameWithoutExtension(grpPath);
        for (int i = 0; i < frameCount; i++)
        {
            var frame = DecodeFrame(reader, frames[i], maxWidth, maxHeight);
            string outputPath = Path.Combine(outputDir, $"{baseName}_{i:D4}.png");
            frame.SaveAsPng(outputPath);
            frame.Dispose();

            Console.WriteLine($"Saved frame {i + 1}/{frameCount}: {outputPath}");
        }
    }
    
    private Image<Rgba32> DecodeFrame(
        BinaryReader reader,
        FrameHeader header,
        int canvasWidth,
        int canvasHeight)
    {
        var image = new Image<Rgba32>(canvasWidth, canvasHeight, Color.Transparent);

        if (header.Width == 0 || header.Height == 0)
            return image;

        long frameOffset = header.DataOffset;
        reader.BaseStream.Seek(frameOffset, SeekOrigin.Begin);

        // Odczytaj tablicę offsetów linii
        var lineOffsets = new ushort[header.Height];
        for (int y = 0; y < header.Height; y++)
            lineOffsets[y] = reader.ReadUInt16();

        // Offsety są względem początku KLATKI (frameOffset), nie lineDataStart!
        for (int y = 0; y < header.Height; y++)
        {
            long linePos = frameOffset + lineOffsets[y];

            if (linePos >= reader.BaseStream.Length)
                continue;

            reader.BaseStream.Seek(linePos, SeekOrigin.Begin);
            DecodeLine(reader, image, header, y, canvasWidth);
        }

        return image;
    }
    
    private void DecodeLine(
        BinaryReader reader,
        Image<Rgba32> image,
        FrameHeader header,
        int y,
        int canvasWidth,
        long maxBytes = long.MaxValue) // domyślnie bez limitu
    {
        int canvasY = header.YOffset + y;
        if (canvasY >= image.Height || canvasY < 0) return;

        int x = 0;
        long bytesRead = 0;

        while (x < header.Width && bytesRead < maxBytes)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

            byte ctrl = reader.ReadByte();
            bytesRead++;

            if ((ctrl & 0x80) != 0)
            {
                // Transparent run
                int count = ctrl & 0x7F;
                x += count;
            }
            else if ((ctrl & 0x40) != 0)
            {
                // Repeat run
                int count = ctrl & 0x3F;
                if (count == 0 || bytesRead >= maxBytes) break;

                byte colorIndex = reader.ReadByte();
                bytesRead++;

                for (int i = 0; i < count && x < header.Width; i++, x++)
                {
                    int canvasX = header.XOffset + x;
                    if (canvasX >= 0 && canvasX < canvasWidth && colorIndex != 0)
                        image[canvasX, canvasY] = _palette[colorIndex].ToPixel<Rgba32>();
                }
            }
            else
            {
                // Raw pixels
                int count = ctrl & 0x3F;
                if (count == 0) break;

                for (int i = 0; i < count && x < header.Width; i++, x++)
                {
                    if (bytesRead >= maxBytes) break;
                    if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                    byte colorIndex = reader.ReadByte();
                    bytesRead++;

                    int canvasX = header.XOffset + x;
                    if (canvasX >= 0 && canvasX < canvasWidth && colorIndex != 0)
                        image[canvasX, canvasY] = _palette[colorIndex].ToPixel<Rgba32>();
                }
            }
        }
    }
}

public struct FrameHeader
{
    public byte XOffset;
    public byte YOffset;
    public byte Width;
    public byte Height;
    public uint DataOffset;
}