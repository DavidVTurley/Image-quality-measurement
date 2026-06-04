using Imcheck.Measurement;
using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement.Measurements.Qa62;
using Imcheck.Measurement.Measurements.Uniformity;

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
        return await CliCommandRunner.ExecuteAsync(options);
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
  Imcheck.Cli <image-path> [--target q13|qa62] [--points <points.csv|q13-results.csv>] [--out <results.csv>] [--imcheck-out <results.xls>] [--compare-imcheck <reference.xls>] [--fit-outlier-sigma] [--fit-sigma-min <sigma>] [--fit-sigma-max <sigma>] [--fit-sigma-step <sigma>] [--sample-size <odd-pixels>] [--sampling <pix-per-inch>] [--reject-outliers] [--outlier-sigma <sigma>]
  Imcheck.Cli --generate qa62|munsell|q13 [--out <target.tif>] [--dpi <pixels-per-inch>]
  Imcheck.Cli --analyze white-sheet <image-path> [--out <results.csv>] [--sample-size <odd-pixels-min-33>] [--color-space srgb|adobe-rgb|ecirgbv2] [--quality full|light|extra-light] [--image-size a3|a2|a1|a0]

Examples:
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --out "C:\path\results.csv"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\previous-results.csv" --reject-outliers --out "C:\path\results.csv"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\previous-results.csv" --compare-imcheck "C:\path\noise.xls"
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\previous-results.csv" --compare-imcheck "C:\path\noise.xls" --fit-outlier-sigma
  dotnet run --project src\Imcheck.Cli -- "C:\path\image.tif" --points "C:\path\points.csv" --imcheck-out "C:\path\results.xls"
  dotnet run --project src\Imcheck.Cli -- --target qa62 "C:\path\QA-62.jpg" --out "C:\path\qa62.csv" --imcheck-out "C:\path\qa62.xls"
  dotnet run --project src\Imcheck.Cli -- --generate qa62 --out "C:\path\QA62_Recreation_600dpi.png" --dpi 600
  dotnet run --project src\Imcheck.Cli -- --generate munsell --out "C:\path\Munsell_Linear_Grayscale_600dpi.tif" --dpi 600
  dotnet run --project src\Imcheck.Cli -- --generate q13 --out "C:\path\Kodak_Q13_Grayscale_600dpi.tif" --dpi 600
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

Q13 result CSV files written by this CLI can also be passed to --points. The
SampleCenterX, SampleCenterY, SampleWidth, and SampleHeight columns are reused.
Passing --outlier-sigma also enables outlier rejection.
Use --fit-outlier-sigma with --compare-imcheck to search rejection settings.
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
    string? ImcheckReferencePath,
    bool FitOutlierSigma,
    double FitSigmaMin,
    double FitSigmaMax,
    double FitSigmaStep,
    int SampleSize,
    bool SampleSizeWasProvided,
    bool RejectOutliers,
    double OutlierSigmaThreshold,
    double SamplingPixelsPerInch,
    int Dpi,
    RgbColorSpace ColorSpace,
    UniformityQualityLevel QualityLevel,
    UniformityImagePlaneSize ImagePlaneSize)
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
        string? imcheckReferencePath = null;
        var fitOutlierSigma = false;
        var fitSigmaMin = 2.0;
        var fitSigmaMax = 6.0;
        var fitSigmaStep = 0.5;
        var sampleSize = 39;
        var sampleSizeWasProvided = false;
        var rejectOutliers = false;
        var outlierSigmaThreshold = 3.0;
        var samplingPixelsPerInch = 301.1;
        var dpi = Qa62TargetGenerator.DefaultDpi;
        var colorSpace = RgbColorSpace.SRgb;
        var qualityLevel = UniformityQualityLevel.Full;
        var imagePlaneSize = UniformityImagePlaneSize.UpToA3;

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
                        "munsell" or "mlg" or "munsell-linear-grayscale" => GenerationTarget.MunsellLinearGrayscale,
                        "q13" or "kodak-q13" or "q13-grayscale" => GenerationTarget.Q13Grayscale,
                        _ => throw new ArgumentException("--generate must be qa62, munsell, or q13.")
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
                case "--compare-imcheck":
                    imcheckReferencePath = RequiredValue(args, ref i, arg);
                    break;
                case "--fit-outlier-sigma":
                    fitOutlierSigma = true;
                    break;
                case "--fit-sigma-min":
                    var rawFitSigmaMin = RequiredValue(args, ref i, arg);
                    if (!double.TryParse(rawFitSigmaMin, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fitSigmaMin))
                    {
                        throw new ArgumentException("--fit-sigma-min must be a number.");
                    }
                    break;
                case "--fit-sigma-max":
                    var rawFitSigmaMax = RequiredValue(args, ref i, arg);
                    if (!double.TryParse(rawFitSigmaMax, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fitSigmaMax))
                    {
                        throw new ArgumentException("--fit-sigma-max must be a number.");
                    }
                    break;
                case "--fit-sigma-step":
                    var rawFitSigmaStep = RequiredValue(args, ref i, arg);
                    if (!double.TryParse(rawFitSigmaStep, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fitSigmaStep))
                    {
                        throw new ArgumentException("--fit-sigma-step must be a number.");
                    }
                    break;
                case "--sample-size":
                    var rawSampleSize = RequiredValue(args, ref i, arg);
                    if (!int.TryParse(rawSampleSize, out sampleSize))
                    {
                        throw new ArgumentException("--sample-size must be an integer.");
                    }
                    sampleSizeWasProvided = true;
                    break;
                case "--reject-outliers":
                    rejectOutliers = true;
                    break;
                case "--outlier-sigma":
                    var rawOutlierSigma = RequiredValue(args, ref i, arg);
                    if (!double.TryParse(rawOutlierSigma, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out outlierSigmaThreshold))
                    {
                        throw new ArgumentException("--outlier-sigma must be a number.");
                    }
                    rejectOutliers = true;
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
                        "full" => UniformityQualityLevel.Full,
                        "light" => UniformityQualityLevel.Light,
                        "extra-light" or "extralight" => UniformityQualityLevel.ExtraLight,
                        _ => throw new ArgumentException("--quality must be full, light, or extra-light.")
                    };
                    break;
                case "--image-size":
                    var rawImageSize = RequiredValue(args, ref i, arg);
                    imagePlaneSize = rawImageSize.ToLowerInvariant() switch
                    {
                        "a3" or "<=a3" => UniformityImagePlaneSize.UpToA3,
                        "a2" or "<=a2" => UniformityImagePlaneSize.UpToA2,
                        "a1" or "<=a1" => UniformityImagePlaneSize.UpToA1,
                        "a0" or "<=a0" => UniformityImagePlaneSize.UpToA0,
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

            if (pointsPath is not null || imcheckTextPath is not null || imcheckReferencePath is not null || fitOutlierSigma)
            {
                throw new ArgumentException("--points, --imcheck-out, --compare-imcheck, and --fit-outlier-sigma are not supported with --generate.");
            }

            if (sampleSizeWasProvided || rejectOutliers || outlierSigmaThreshold != 3.0 || samplingPixelsPerInch != 301.1)
            {
                throw new ArgumentException("--sample-size, --reject-outliers, --outlier-sigma, and --sampling are only supported when measuring images.");
            }

            if (dpi <= 0)
            {
                throw new ArgumentException("--dpi must be positive.");
            }

            return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, imcheckReferencePath, fitOutlierSigma, fitSigmaMin, fitSigmaMax, fitSigmaStep, sampleSize, sampleSizeWasProvided, rejectOutliers, outlierSigmaThreshold, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
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

            if (pointsPath is not null || imcheckTextPath is not null || imcheckReferencePath is not null || fitOutlierSigma)
            {
                throw new ArgumentException("--points, --imcheck-out, --compare-imcheck, and --fit-outlier-sigma are not supported with --analyze.");
            }

            if (rejectOutliers || outlierSigmaThreshold != 3.0 || samplingPixelsPerInch != 301.1)
            {
                throw new ArgumentException("--reject-outliers, --outlier-sigma, and --sampling are only supported when measuring targets.");
            }

            if (sampleSize < 33 || sampleSize % 2 == 0)
            {
                throw new ArgumentException("--sample-size must be an odd integer of at least 33 for white-sheet analysis.");
            }

            return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, imcheckReferencePath, fitOutlierSigma, fitSigmaMin, fitSigmaMax, fitSigmaStep, sampleSize, sampleSizeWasProvided, rejectOutliers, outlierSigmaThreshold, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
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

        if (imcheckReferencePath is not null && !File.Exists(imcheckReferencePath))
        {
            throw new FileNotFoundException("ImCheck reference file was not found.", imcheckReferencePath);
        }

        if (target == MeasurementTarget.Qa62 && pointsPath is not null)
        {
            throw new ArgumentException("--points is only supported with --target q13.");
        }

        if (target == MeasurementTarget.Qa62 && imcheckReferencePath is not null)
        {
            throw new ArgumentException("--compare-imcheck is only supported with --target q13.");
        }

        if (target == MeasurementTarget.Qa62 && fitOutlierSigma)
        {
            throw new ArgumentException("--fit-outlier-sigma is only supported with --target q13.");
        }

        if (fitOutlierSigma && imcheckReferencePath is null)
        {
            throw new ArgumentException("--fit-outlier-sigma requires --compare-imcheck.");
        }

        if (sampleSize <= 0 || sampleSize % 2 == 0)
        {
            throw new ArgumentException("--sample-size must be a positive odd integer.");
        }

        if (samplingPixelsPerInch <= 0)
        {
            throw new ArgumentException("--sampling must be positive.");
        }

        if (outlierSigmaThreshold <= 0)
        {
            throw new ArgumentException("--outlier-sigma must be positive.");
        }

        if (fitSigmaMin <= 0 || fitSigmaMax <= 0)
        {
            throw new ArgumentException("--fit-sigma-min and --fit-sigma-max must be positive.");
        }

        if (fitSigmaMax < fitSigmaMin)
        {
            throw new ArgumentException("--fit-sigma-max must be greater than or equal to --fit-sigma-min.");
        }

        if (fitSigmaStep <= 0)
        {
            throw new ArgumentException("--fit-sigma-step must be positive.");
        }

        return new CliOptions(imagePath, target, generateTarget, analysisTarget, pointsPath, csvPath, imcheckTextPath, imcheckReferencePath, fitOutlierSigma, fitSigmaMin, fitSigmaMax, fitSigmaStep, sampleSize, sampleSizeWasProvided, rejectOutliers, outlierSigmaThreshold, samplingPixelsPerInch, dpi, colorSpace, qualityLevel, imagePlaneSize);
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
    Qa62,
    MunsellLinearGrayscale,
    Q13Grayscale
}

internal enum AnalysisMode
{
    WhiteSheet
}
