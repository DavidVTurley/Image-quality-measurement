using Imcheck.Measurement.Measurements.Uniformity;
using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class UniformityAnalyzerTests
{
    [Fact]
    public void UniformWhiteSheetPassesFullA3Tolerances()
    {
        var path = CreateTempImage(width: 200, height: 120, (_, _) => new Vec3b(242, 242, 242));
        try
        {
            var result = new UniformityAnalyzer().Analyze(path);

            Assert.Equal(5, result.Samples.Count);
            Assert.Equal(33, result.SampleSize);
            Assert.All(result.Samples, sample => Assert.Equal(33, sample.SampleSize));
            Assert.True(result.IlluminationPass);
            Assert.True(result.WhiteBalancePass);
            Assert.Equal(0, result.MaxDeltaLStar, precision: 4);
            Assert.Equal(0, result.MaxDeltaEab, precision: 4);
            Assert.Contains("Max delta L*", result.ToCsv());
            Assert.Contains("Name,MeanRed,MeanGreen,MeanBlue,LStar,AStar,BStar,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight", result.ToCsv());
            Assert.DoesNotContain("SampleTopLeftX", result.ToCsv());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AutoSampleSizeUsesThirtyThreePercentOfGridCellWithMinimum()
    {
        var path = CreateTempImage(width: 300, height: 300, (_, _) => new Vec3b(242, 242, 242));
        try
        {
            var result = new UniformityAnalyzer().Analyze(path);

            Assert.Equal(33, result.SampleSize);
            Assert.Equal(("TopLeft", 50.0, 50.0, 33), SampleShape(result.Samples[0]));
            Assert.Equal(("TopRight", 250.0, 50.0, 33), SampleShape(result.Samples[1]));
            Assert.Equal(("Center", 150.0, 150.0, 33), SampleShape(result.Samples[2]));
            Assert.Equal(("BottomLeft", 50.0, 250.0, 33), SampleShape(result.Samples[3]));
            Assert.Equal(("BottomRight", 250.0, 250.0, 33), SampleShape(result.Samples[4]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExplicitSampleSizeOverridesAutoSize()
    {
        var path = CreateTempImage(width: 300, height: 300, (_, _) => new Vec3b(242, 242, 242));
        try
        {
            var result = new UniformityAnalyzer().Analyze(
                path,
                new UniformityAnalysisOptions { SampleSize = 51 });

            Assert.Equal(51, result.SampleSize);
            Assert.All(result.Samples, sample => Assert.Equal(51, sample.SampleSize));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExplicitSampleSizeBelowMinimumFails()
    {
        var path = CreateTempImage(width: 300, height: 300, (_, _) => new Vec3b(242, 242, 242));
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UniformityAnalyzer().Analyze(
                    path,
                    new UniformityAnalysisOptions { SampleSize = 31 }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LuminanceGradientFailsFullA3IlluminationTolerance()
    {
        var path = CreateTempImage(
            width: 200,
            height: 120,
            (x, _) =>
            {
                var value = (byte)(230 - x / 8);
                return new Vec3b(value, value, value);
            });
        try
        {
            var result = new UniformityAnalyzer().Analyze(path);

            Assert.False(result.IlluminationPass);
            Assert.True(result.WhiteBalancePass);
            Assert.True(result.MaxDeltaLStar > 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ColorCastGradientFailsWhiteBalanceTolerance()
    {
        var path = CreateTempImage(
            width: 200,
            height: 120,
            (x, _) =>
            {
                var blue = (byte)230;
                var green = (byte)230;
                var red = (byte)(230 - x / 8);
                return new Vec3b(blue, green, red);
            });
        try
        {
            var result = new UniformityAnalyzer().Analyze(path);

            Assert.False(result.WhiteBalancePass);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToleranceTableMatchesWhiteSheetUniformityRequirements()
    {
        Assert.Equal(3.0, UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.Full, UniformityImagePlaneSize.UpToA3));
        Assert.Equal(4.0, UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.Full, UniformityImagePlaneSize.UpToA2));
        Assert.Equal(5.0, UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.Full, UniformityImagePlaneSize.UpToA1));
        Assert.Equal(6.0, UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.Full, UniformityImagePlaneSize.UpToA0));
        Assert.Equal(3.0, UniformityTolerances.WhiteBalanceDeltaEab(UniformityQualityLevel.Full));

        Assert.Equal(5.0, UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.ExtraLight, UniformityImagePlaneSize.UpToA3));
        Assert.Null(UniformityTolerances.IlluminationDeltaL(UniformityQualityLevel.ExtraLight, UniformityImagePlaneSize.UpToA2));
        Assert.Equal(5.0, UniformityTolerances.WhiteBalanceDeltaEab(UniformityQualityLevel.ExtraLight));
    }

    [Fact]
    public void SavedUniformityReportCanBeReloadedAndReproduced()
    {
        AssertUniformityReportCanBeReloadedAndReproduced("white", (_, _) => new Vec3b(255, 255, 255));
        AssertUniformityReportCanBeReloadedAndReproduced("black", (_, _) => new Vec3b(0, 0, 0));
        AssertUniformityReportCanBeReloadedAndReproduced("midgray", (_, _) => new Vec3b(127, 127, 127));
        AssertUniformityReportCanBeReloadedAndReproduced(
            "varying",
            (x, y) =>
            {
                var blue = (byte)((x * 7 + y * 11) % 256);
                var green = (byte)((x * 13 + y * 3) % 256);
                var red = (byte)((x * 5 + y * 17) % 256);
                return new Vec3b(blue, green, red);
            });
    }

    private static void AssertUniformityReportCanBeReloadedAndReproduced(string name, Func<int, int, Vec3b> pixel)
    {
        var directory = Directory.CreateTempSubdirectory("imcheck-uniformity-roundtrip-");
        var imagePath = Path.Combine(directory.FullName, $"{name}.png");
        var firstCsvPath = Path.Combine(directory.FullName, $"{name}-first.csv");
        var secondCsvPath = Path.Combine(directory.FullName, $"{name}-second.csv");
        try
        {
            using var image = new Mat(180, 240, MatType.CV_8UC3);
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    image.Set(y, x, pixel(x, y));
                }
            }

            Cv2.ImWrite(imagePath, image);
            var analyzer = new UniformityAnalyzer();
            var firstResult = analyzer.Analyze(imagePath);
            File.WriteAllText(firstCsvPath, firstResult.ToCsv());

            var importedSamples = UniformityResultSampleCsv.LoadSamples(firstCsvPath);
            var secondResult = analyzer.Analyze(imagePath, new UniformityAnalysisOptions
            {
                Samples = importedSamples
            });
            File.WriteAllText(secondCsvPath, secondResult.ToCsv());

            Assert.Equal(File.ReadAllText(firstCsvPath), File.ReadAllText(secondCsvPath));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string CreateTempImage(int width, int height, Func<int, int, Vec3b> pixel)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var image = new Mat(height, width, MatType.CV_8UC3);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.Set(y, x, pixel(x, y));
            }
        }

        Cv2.ImWrite(path, image);
        return path;
    }

    private static (string Name, double CenterX, double CenterY, int SampleSize) SampleShape(WhiteSheetSampleMeasurement sample)
    {
        return (sample.Name, sample.SampleCenterX, sample.SampleCenterY, sample.SampleSize);
    }
}
