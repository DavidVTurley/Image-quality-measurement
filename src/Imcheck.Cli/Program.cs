using Imcheck.Measurement;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
    {
        PrintUsage();
        return args.Length == 0 ? 1 : 0;
    }

    try
    {
        var options = CliOptions.Parse(args);
        IReadOnlyList<Q13SamplePoint>? sampleCenters = null;
        if (options.PointsPath is not null)
        {
            sampleCenters = Q13SamplePointCsv.Load(options.PointsPath);
        }

        var result = new Q13Measurer().Measure(
            options.ImagePath,
            new Q13MeasurementOptions
            {
                SampleSize = options.SampleSize,
                SampleCenters = sampleCenters
            });

        if (options.CsvPath is not null)
        {
            await File.WriteAllTextAsync(options.CsvPath, result.ToCsv());
        }

        if (options.ImcheckTextPath is not null)
        {
            await File.WriteAllTextAsync(options.ImcheckTextPath, result.ToImcheckText());
        }

        if (options.CsvPath is null && options.ImcheckTextPath is null)
        {
            Console.Write(result.ToCsv());
        }
        else
        {
            Console.WriteLine($"Measured {result.ImageName}: {result.Patches.Count} patches, N={result.SampleDataSize}, 1/gamma={result.InverseGamma:0.00}");
            if (options.CsvPath is not null) Console.WriteLine($"CSV: {options.CsvPath}");
            if (options.ImcheckTextPath is not null) Console.WriteLine($"Imcheck text: {options.ImcheckTextPath}");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }
}

static void PrintUsage()
{
    Console.WriteLine("""
Imcheck.Cli - Kodak Q-13 measurement

Usage:
  Imcheck.Cli <image-path> [--points <points.csv>] [--out <results.csv>] [--imcheck-out <results.xls>] [--sample-size <odd-pixels>]

Examples:
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --out "C:\path\results.csv"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --imcheck-out "C:\path\results.xls"

Points CSV:
  Patch,X,Y
  0,31.31,69.48
  ...

or:
  X,Y
  31.31,69.48
  ...
""");
}

internal sealed record CliOptions(
    string ImagePath,
    string? PointsPath,
    string? CsvPath,
    string? ImcheckTextPath,
    int SampleSize)
{
    public static CliOptions Parse(string[] args)
    {
        string? imagePath = null;
        string? pointsPath = null;
        string? csvPath = null;
        string? imcheckTextPath = null;
        var sampleSize = 39;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--points":
                    pointsPath = RequiredValue(args, ref i, arg);
                    break;
                case "--out":
                    csvPath = RequiredValue(args, ref i, arg);
                    break;
                case "--imcheck-out":
                    imcheckTextPath = RequiredValue(args, ref i, arg);
                    break;
                case "--sample-size":
                    var rawSampleSize = RequiredValue(args, ref i, arg);
                    if (!int.TryParse(rawSampleSize, out sampleSize))
                    {
                        throw new ArgumentException("--sample-size must be an integer.");
                    }
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unknown option: {arg}");
                    }

                    if (imagePath is not null)
                    {
                        throw new ArgumentException("Only one image path can be provided.");
                    }

                    imagePath = arg;
                    break;
            }
        }

        if (imagePath is null)
        {
            throw new ArgumentException("Image path is required.");
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file was not found.", imagePath);
        }

        if (pointsPath is not null && !File.Exists(pointsPath))
        {
            throw new FileNotFoundException("Points file was not found.", pointsPath);
        }

        if (sampleSize <= 0 || sampleSize % 2 == 0)
        {
            throw new ArgumentException("--sample-size must be a positive odd integer.");
        }

        return new CliOptions(imagePath, pointsPath, csvPath, imcheckTextPath, sampleSize);
    }

    private static string RequiredValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index++;
        return args[index];
    }
}
