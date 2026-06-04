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
        Q13ImportedSamples? importedSamples = null;
        IReadOnlyList<Q13SamplePoint>? sampleCenters = null;
        if (options.PointsPath is not null)
        {
            if (Q13ResultSampleCsv.IsResultCsv(options.PointsPath))
            {
                importedSamples = Q13ResultSampleCsv.Load(options.PointsPath);
                sampleCenters = importedSamples.Centers;
            }
            else
            {
                sampleCenters = Q13SamplePointCsv.Load(options.PointsPath);
            }
        }

        Q13MeasurementResult Measure(bool rejectOutliers, double outlierSigmaThreshold)
        {
            return new Q13Measurer().Measure(
                options.ImagePath!,
                new Q13MeasurementOptions
                {
                    SampleSize = options.SampleSizeWasProvided ? options.SampleSize : importedSamples?.SampleSize ?? options.SampleSize,
                    SampleCenters = sampleCenters,
                    UseOutlierRejection = rejectOutliers,
                    OutlierSigmaThreshold = outlierSigmaThreshold
                });
        }

        var result = Measure(options.RejectOutliers, options.OutlierSigmaThreshold);

        await WriteOptionalOutputsAsync(options, result.ToCsv(), result.ToImcheckText());

        if (options.ImcheckReferencePath is not null)
        {
            PrintQ13ImcheckComparison(result, options.ImcheckReferencePath);
            if (options.FitOutlierSigma)
            {
                FitQ13OutlierSigma(Measure, options);
            }
        }

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

        if (options.CsvPath is null && options.ImcheckTextPath is null && options.ImcheckReferencePath is null)
        {
            Console.Write(csv);
        }
    }

    private static void PrintQ13ImcheckComparison(Q13MeasurementResult result, string referencePath)
    {
        var reference = Q13ImcheckNoiseReference.Load(referencePath);
        var comparison = reference.Compare(result);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"ImCheck comparison: avg mean error={comparison.AverageMeanError:0.0000}, avg noise error={comparison.AverageNoiseError:0.0000}, combined={comparison.CombinedAverageError:0.0000}, max={comparison.MaxError:0.0000} at patch {comparison.MaxErrorPatch}"));
    }

    private static void FitQ13OutlierSigma(Func<bool, double, Q13MeasurementResult> measure, CliOptions options)
    {
        var reference = Q13ImcheckNoiseReference.Load(options.ImcheckReferencePath!);
        Q13ImcheckNoiseComparison? bestComparison = null;
        var bestRejectOutliers = options.RejectOutliers;
        var bestOutlierSigma = options.OutlierSigmaThreshold;

        var outlierSettings = options.FitOutlierSigma
            ? FitOutlierSettings(options)
            : [new OutlierFitSetting(options.RejectOutliers, options.OutlierSigmaThreshold)];

        foreach (var outlierSetting in outlierSettings)
        {
            var comparison = reference.Compare(measure(outlierSetting.RejectOutliers, outlierSetting.SigmaThreshold));
            if (bestComparison is null || comparison.CombinedAverageError < bestComparison.CombinedAverageError)
            {
                bestComparison = comparison;
                bestRejectOutliers = outlierSetting.RejectOutliers;
                bestOutlierSigma = outlierSetting.SigmaThreshold;
            }
        }

        var rejectionLabel = bestRejectOutliers
            ? string.Create(CultureInfo.InvariantCulture, $"sigma={bestOutlierSigma:0.###}")
            : "none";
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Best fitted measurement: outlier rejection={rejectionLabel}, avg mean error={bestComparison!.AverageMeanError:0.0000}, avg noise error={bestComparison.AverageNoiseError:0.0000}, combined={bestComparison.CombinedAverageError:0.0000}, max={bestComparison.MaxError:0.0000} at patch {bestComparison.MaxErrorPatch}"));
    }

    private static IEnumerable<OutlierFitSetting> FitOutlierSettings(CliOptions options)
    {
        yield return new OutlierFitSetting(false, options.OutlierSigmaThreshold);
        foreach (var sigmaThreshold in FitRangeValues(options.FitSigmaMin, options.FitSigmaMax, options.FitSigmaStep))
        {
            yield return new OutlierFitSetting(true, sigmaThreshold);
        }
    }

    private static IEnumerable<double> FitRangeValues(double start, double end, double step)
    {
        for (var value = start; value <= end + step / 10.0; value += step)
        {
            yield return Math.Round(value, 10);
        }
    }

    private sealed record OutlierFitSetting(bool RejectOutliers, double SigmaThreshold);

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
