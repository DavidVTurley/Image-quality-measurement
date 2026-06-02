using Imcheck.Measurement;
using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement.Measurements.Qa62;
using Imcheck.Measurement.Measurements.Uniformity;
using System.Globalization;

internal static class CliCommandRunner
{
    public static async Task<int> ExecuteAsync(CliOptions options)
    {
        if (options.GenerateTarget is not null)
        {
            return ExecuteGenerate(options);
        }

        if (options.AnalysisTarget == AnalysisMode.WhiteSheet)
        {
            return await ExecuteUniformityAsync(options);
        }

        if (options.Target == MeasurementTarget.Qa62)
        {
            return await ExecuteQa62Async(options);
        }

        return await ExecuteQ13Async(options);
    }

    private static int ExecuteGenerate(CliOptions options)
    {
        switch (options.GenerateTarget)
        {
            case GenerationTarget.Qa62:
            {
                var outputPath = options.CsvPath ?? Path.Combine(Environment.CurrentDirectory, "QA62_Recreation_600dpi.png");
                var result = new Qa62TargetGenerator().Generate(outputPath, new Qa62TargetGeneratorOptions { Dpi = options.Dpi });
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Generated QA-62 target: {result.OutputPath} ({result.Width}x{result.Height}, dpi intent={result.Dpi})"));
                return 0;
            }
            case GenerationTarget.MunsellLinearGrayscale:
            {
                var outputPath = options.CsvPath ?? Path.Combine(Environment.CurrentDirectory, "Munsell_Linear_Grayscale_600dpi.tif");
                var result = new MunsellLinearGrayscaleTargetGenerator().Generate(outputPath, new MunsellLinearGrayscaleTargetGeneratorOptions { Dpi = options.Dpi });
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Generated Munsell Linear Grayscale target: {result.OutputPath} ({result.Width}x{result.Height}, dpi intent={result.Dpi})"));
                return 0;
            }
            case GenerationTarget.Q13Grayscale:
            {
                var outputPath = options.CsvPath ?? Path.Combine(Environment.CurrentDirectory, "Kodak_Q13_Grayscale_600dpi.tif");
                var result = new Q13GrayscaleTargetGenerator().Generate(outputPath, new Q13GrayscaleTargetGeneratorOptions { Dpi = options.Dpi });
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Generated Kodak Q13 Grayscale target: {result.OutputPath} ({result.Width}x{result.Height}, dpi intent={result.Dpi})"));
                return 0;
            }
            default:
                throw new InvalidOperationException("Unknown generation target.");
        }
    }

    private static async Task<int> ExecuteUniformityAsync(CliOptions options)
    {
        var result = new UniformityAnalyzer().Analyze(
            options.ImagePath!,
            new UniformityAnalysisOptions
            {
                SampleSize = options.SampleSizeWasProvided ? options.SampleSize : null,
                ColorSpace = options.ColorSpace,
                QualityLevel = options.QualityLevel,
                ImagePlaneSize = options.ImagePlaneSize
            });

        await WritePrimaryOutputAsync(options.CsvPath, result.ToCsv(), "White-sheet analysis");
        return 0;
    }

    private static async Task<int> ExecuteQa62Async(CliOptions options)
    {
        var result = new Qa62Measurer().Measure(
            options.ImagePath!,
            new Qa62MeasurementOptions
            {
                SampleSize = options.SampleSizeWasProvided ? options.SampleSize : null,
                SamplingPixelsPerInch = options.SamplingPixelsPerInch
            });

        await WriteOptionalOutputsAsync(options, result.ToCsv(), result.ToImcheckText());

        if (options.CsvPath is null && options.ImcheckTextPath is null)
        {
            return 0;
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Measured {result.ImageName}: {result.Patches.Count} QA-62 patches, sampling={result.SamplingPixelsPerInch:0.0} pix/inch"));
        PrintOutputPaths(options);
        return 0;
    }

    private static async Task<int> ExecuteQ13Async(CliOptions options)
    {
        var sampleCenters = options.PointsPath is null
            ? null
            : Q13SamplePointCsv.Load(options.PointsPath);

        var result = new Q13Measurer().Measure(
            options.ImagePath!,
            new Q13MeasurementOptions
            {
                SampleSize = options.SampleSize,
                SampleCenters = sampleCenters
            });

        await WriteOptionalOutputsAsync(options, result.ToCsv(), result.ToImcheckText());

        if (options.CsvPath is null && options.ImcheckTextPath is null)
        {
            return 0;
        }

        Console.WriteLine($"Measured {result.ImageName}: {result.Patches.Count} patches, N={result.SampleDataSize}, 1/gamma={result.InverseGamma:0.00}");
        PrintOutputPaths(options);
        return 0;
    }

    private static async Task WritePrimaryOutputAsync(string? path, string content, string label)
    {
        if (path is not null)
        {
            await File.WriteAllTextAsync(path, content);
            Console.WriteLine($"{label}: {path}");
            return;
        }

        Console.Write(content);
    }

    private static async Task WriteOptionalOutputsAsync(CliOptions options, string csv, string imcheckText)
    {
        if (options.CsvPath is not null)
        {
            await File.WriteAllTextAsync(options.CsvPath, csv);
        }

        if (options.ImcheckTextPath is not null)
        {
            await File.WriteAllTextAsync(options.ImcheckTextPath, imcheckText);
        }

        if (options.CsvPath is null && options.ImcheckTextPath is null)
        {
            Console.Write(csv);
        }
    }

    private static void PrintOutputPaths(CliOptions options)
    {
        if (options.CsvPath is not null)
        {
            Console.WriteLine($"CSV: {options.CsvPath}");
        }

        if (options.ImcheckTextPath is not null)
        {
            Console.WriteLine($"Imcheck text: {options.ImcheckTextPath}");
        }
    }
}
