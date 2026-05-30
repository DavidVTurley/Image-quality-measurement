using Imcheck.Measurement;
using Imcheck.Measurement.Meaasurements.Q13;
using Imcheck.Measurement.Meaasurements.Uniformity;

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
        if (options.GenerateTarget == GenerationTarget.Qa62)
        {
            var outputPath = options.CsvPath ?? Path.Combine(Environment.CurrentDirectory, "QA62_Recreation_600dpi.png");
            var result = new Qa62TargetGenerator().Generate(
                outputPath,
                new Qa62TargetGeneratorOptions { Dpi = options.Dpi });

            Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Generated QA-62 target: {result.OutputPath} ({result.Width}x{result.Height}, dpi intent={result.Dpi})"));
            return 0;
        }

        if (options.AnalysisTarget == AnalysisMode.WhiteSheet)
        {
            var result = new MetamorfozeWhiteSheetAnalyzer().Analyze(
                options.ImagePath!,
                new MetamorfozeWhiteSheetAnalysisOptions
                {
                    SampleSize = options.SampleSizeWasProvided ? options.SampleSize : null,
                    ColorSpace = options.ColorSpace,
                    QualityLevel = options.QualityLevel,
                    ImagePlaneSize = options.ImagePlaneSize
                });

            if (options.CsvPath is not null)
            {
                await File.WriteAllTextAsync(options.CsvPath, result.ToCsv());
                Console.WriteLine($"White-sheet analysis: {options.CsvPath}");
            }
            else
            {
                Console.Write(result.ToCsv());
            }

            return 0;
        }

        if (options.Target == MeasurementTarget.Qa62)
        {
            var result = new Qa62Measurer().Measure(
                options.ImagePath!,
                new Qa62MeasurementOptions
                {
                    SampleSize = options.SampleSizeWasProvided ? options.SampleSize : null,
                    SamplingPixelsPerInch = options.SamplingPixelsPerInch
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
                Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Measured {result.ImageName}: {result.Patches.Count} QA-62 patches, sampling={result.SamplingPixelsPerInch:0.0} pix/inch"));
                if (options.CsvPath is not null) Console.WriteLine($"CSV: {options.CsvPath}");
                if (options.ImcheckTextPath is not null) Console.WriteLine($"Imcheck text: {options.ImcheckTextPath}");
            }

            return 0;
        }

        IReadOnlyList<Q13SamplePoint>? sampleCenters = null;
        if (options.PointsPath is not null)
        {
            sampleCenters = Q13SamplePointCsv.Load(options.PointsPath);
        }

        var q13Result = new Q13Measurer().Measure(
            options.ImagePath!,
            new Q13MeasurementOptions
            {
                SampleSize = options.SampleSize,
                SampleCenters = sampleCenters
            });

        if (options.CsvPath is not null)
        {
            await File.WriteAllTextAsync(options.CsvPath, q13Result.ToCsv());
        }

        if (options.ImcheckTextPath is not null)
        {
            await File.WriteAllTextAsync(options.ImcheckTextPath, q13Result.ToImcheckText());
        }

        if (options.CsvPath is null && options.ImcheckTextPath is null)
        {
            Console.Write(q13Result.ToCsv());
        }
        else
        {
            Console.WriteLine($"Measured {q13Result.ImageName}: {q13Result.Patches.Count} patches, N={q13Result.SampleDataSize}, 1/gamma={q13Result.InverseGamma:0.00}");
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
Imcheck.Cli - Imcheck-style target measurement

Usage:
  Imcheck.Cli <image-path> [--target q13|qa62] [--points <points.csv>] [--out <results.csv>] [--imcheck-out <results.xls>] [--sample-size <odd-pixels>] [--sampling <pix-per-inch>]
  Imcheck.Cli --generate qa62 [--out <target.png>] [--dpi <pixels-per-inch>]
  Imcheck.Cli --analyze white-sheet <image-path> [--out <results.csv>] [--sample-size <odd-pixels-min-33>] [--color-space srgb|adobe-rgb|ecirgbv2] [--quality full|light|extra-light] [--image-size a3|a2|a1|a0]

Examples:
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --out "C:\path\results.csv"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --imcheck-out "C:\path\results.xls"
  dotnet run --project src\Imcheck.Cli -- --target qa62 "C:\path\QA-62.jpg" --out "C:\path\qa62.csv" --imcheck-out "C:\path\qa62.xls"
  dotnet run --project src\Imcheck.Cli -- --generate qa62 --out "C:\path\QA62_Recreation_600dpi.png" --dpi 600
  dotnet run --project src\Imcheck.Cli -- --analyze white-sheet "C:\path\white-sheet.tif" --out "C:\path\white-sheet.csv" --color-space ecirgbv2 --quality full --image-size a3

White-sheet analysis:
  Uses five square areas from a 3x3 grid: four corner cells and the center cell.
  Default area size is 33% of the smaller grid-cell dimension, with a 33x33 pixel minimum.

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
    string? ImagePath,
    MeasurementTarget Target,
    GenerationTarget? GenerateTarget,
    AnalysisMode? AnalysisTarget,
    string? PointsPath,
    string? CsvPath,
    string? ImcheckTextPath,
    int SampleSize,
    bool SampleSizeWasProvided,
    double SamplingPixelsPerInch,
    int Dpi,
    RgbColorSpace ColorSpace,
    MetamorfozeQualityLevel QualityLevel,
    MetamorfozeImagePlaneSize ImagePlaneSize)
{
    public static CliOptions Parse(string[] args)
    {
        string? imagePath = null;
        var target = MeasurementTarget.Q13;
        GenerationTarget? generateTarget = null;
        AnalysisMode? analysisTarget = null;
        string? pointsPath = null;
        string? csvPath = null;
        string? imcheckTextPath = null;
        var sampleSize = 39;
        var sampleSizeWasProvided = false;
        var samplingPixelsPerInch = 301.1;
        var dpi = Qa62TargetGenerator.DefaultDpi;
        var colorSpace = RgbColorSpace.SRgb;
        var qualityLevel = MetamorfozeQualityLevel.Full;
        var imagePlaneSize = MetamorfozeImagePlaneSize.UpToA3;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--target":
                    var rawTarget = RequiredValue(args, ref i, arg);
                    target = rawTarget.ToLowerInvariant() switch
                    {
                        "q13" => MeasurementTarget.Q13,
                        "qa62" or "qa-62" => MeasurementTarget.Qa62,
                        _ => throw new ArgumentException("--target must be q13 or qa62.")
                    };
                    break;
                case "--generate":
                    var rawGenerateTarget = RequiredValue(args, ref i, arg);
                    generateTarget = rawGenerateTarget.ToLowerInvariant() switch
                    {
                        "qa62" or "qa-62" => GenerationTarget.Qa62,
                        _ => throw new ArgumentException("--generate only supports qa62.")
                    };
                    break;
                case "--analyze":
                    var rawAnalysisTarget = RequiredValue(args, ref i, arg);
                    analysisTarget = rawAnalysisTarget.ToLowerInvariant() switch
                    {
                        "white-sheet" or "whitesheet" or "white" => AnalysisMode.WhiteSheet,
                        _ => throw new ArgumentException("--analyze only supports white-sheet.")
                    };
                    break;
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
                    sampleSizeWasProvided = true;
                    break;
                case "--sampling":
                    var rawSampling = RequiredValue(args, ref i, arg);
                    if (!double.TryParse(rawSampling, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out samplingPixelsPerInch))
                    {
                        throw new ArgumentException("--sampling must be a number.");
                    }
                    break;
                case "--dpi":
                    var rawDpi = RequiredValue(args, ref i, arg);
                    if (!int.TryParse(rawDpi, out dpi))
                    {
                        throw new ArgumentException("--dpi must be an integer.");
                    }
                    break;
                case "--color-space":
                    var rawColorSpace = RequiredValue(args, ref i, arg);
                    colorSpace = rawColorSpace.ToLowerInvariant() switch
                    {
                        "srgb" or "s-rgb" => RgbColorSpace.SRgb,
                        "adobe-rgb" or "adobergb" or "adobe-rgb-1998" or "adobergb1998" => RgbColorSpace.AdobeRgb1998,
                        "ecirgbv2" or "eci-rgb-v2" or "eci-rgb" => RgbColorSpace.EciRgbV2,
                        _ => throw new ArgumentException("--color-space must be srgb, adobe-rgb, or ecirgbv2.")
                    };
                    break;
                case "--quality":
                    var rawQuality = RequiredValue(args, ref i, arg);
                    qualityLevel = rawQuality.ToLowerInvariant() switch
                    {
                        "full" or "metamorfoze" => MetamorfozeQualityLevel.Full,
                        "light" => MetamorfozeQualityLevel.Light,
                        "extra-light" or "extralight" => MetamorfozeQualityLevel.ExtraLight,
                        _ => throw new ArgumentException("--quality must be full, light, or extra-light.")
                    };
                    break;
                case "--image-size":
                    var rawImageSize = RequiredValue(args, ref i, arg);
                    imagePlaneSize = rawImageSize.ToLowerInvariant() switch
                    {
                        "a3" or "<=a3" => MetamorfozeImagePlaneSize.UpToA3,
                        "a2" or "<=a2" => MetamorfozeImagePlaneSize.UpToA2,
                        "a1" or "<=a1" => MetamorfozeImagePlaneSize.UpToA1,
                        "a0" or "<=a0" => MetamorfozeImagePlaneSize.UpToA0,
                        _ => throw new ArgumentException("--image-size must be a3, a2, a1, or a0.")
                    };
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

        if (generateTarget is not null && analysisTarget is not null)
        {
            throw new ArgumentException("--generate and --analyze cannot be used together.");
        }

        if (generateTarget is not null)
        {
            if (imagePath is not null)
            {
                throw new ArgumentException("Image path cannot be provided with --generate.");
            }

            if (pointsPath is not null || imcheckTextPath is not null)
            {
                throw new ArgumentException("--points and --imcheck-out are not supported with --generate.");
            }

            if (sampleSizeWasProvided || samplingPixelsPerInch != 301.1)
            {
                throw new ArgumentException("--sample-size and --sampling are only supported when measuring images.");
            }

            if (dpi <= 0)
            {
                throw new ArgumentException("--dpi must be positive.");
            }

            return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, sampleSize, sampleSizeWasProvided, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
        }

        if (analysisTarget is not null)
        {
            if (imagePath is null)
            {
                throw new ArgumentException("Image path is required.");
            }

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            if (pointsPath is not null || imcheckTextPath is not null)
            {
                throw new ArgumentException("--points and --imcheck-out are not supported with --analyze.");
            }

            if (samplingPixelsPerInch != 301.1)
            {
                throw new ArgumentException("--sampling is only supported when measuring targets.");
            }

            if (sampleSize < 33 || sampleSize % 2 == 0)
            {
                throw new ArgumentException("--sample-size must be an odd integer of at least 33 for white-sheet analysis.");
            }

            return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, sampleSize, sampleSizeWasProvided, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
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

        if (target == MeasurementTarget.Qa62 && pointsPath is not null)
        {
            throw new ArgumentException("--points is only supported with --target q13.");
        }

        if (sampleSize <= 0 || sampleSize % 2 == 0)
        {
            throw new ArgumentException("--sample-size must be a positive odd integer.");
        }

        if (samplingPixelsPerInch <= 0)
        {
            throw new ArgumentException("--sampling must be positive.");
        }

        return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, sampleSize, sampleSizeWasProvided, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
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

internal enum MeasurementTarget
{
    Q13,
    Qa62
}

internal enum GenerationTarget
{
    Qa62
}

internal enum AnalysisMode
{
    WhiteSheet
}
