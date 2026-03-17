namespace GrpConverter;

public class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Użycie:");
            Console.WriteLine("  Osobne pliki:  GrpConverter frames  <plik.grp> <paleta.wpe> <output_dir>");
            Console.WriteLine("  Sprite sheet:  GrpConverter sheet   <plik.grp> <paleta.wpe> <output.png> [columns]");
            return;
        }

        string mode        = args[0];
        string grpPath     = args[1];
        string palettePath = args[2];

        var converter = new GrpConverter(palettePath);

        switch (mode)
        {
            case "frames":
                string outputDir = args[3];
                Directory.CreateDirectory(outputDir);
                converter.Convert(grpPath, outputDir);
                break;

            case "sheet":
                string outputPng = args[3];
                int columns = args.Length > 4 ? int.Parse(args[4]) : 16;
                converter.ConvertToSpriteSheet(grpPath, outputPng, columns);
                break;

            default:
                Console.WriteLine($"Nieznany tryb: {mode}");
                break;
        }
    }
}