using Imcheck.Measurement.Metamorfoze;
using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class MetamorfozeWhiteSheetAnalyzerTests
{
    [Fact]
    public void UniformWhiteSheetPassesFullA3Tolerances()
    {
        var path = CreateTempImage(width: 200, height: 120, (_, _) => new Vec3b(242, 242, 242));
        try
        {
            var result = new MetamorfozeWhiteSheetAnalyzer().Analyze(path);

            Assert.Equal(5, result.Samples.Count);
            Assert.True(result.IlluminationPass);
            Assert.True(result.WhiteBalancePass);
            Assert.Equal(0, result.MaxDeltaLStar, precision: 4);
            Assert.Equal(0, result.MaxDeltaEab, precision: 4);
            Assert.Contains("Illumination,MaxDeltaLStar", result.ToCsv());
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
            var result = new MetamorfozeWhiteSheetAnalyzer().Analyze(path);

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
            var result = new MetamorfozeWhiteSheetAnalyzer().Analyze(path);

            Assert.False(result.WhiteBalancePass);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MetamorfozeV2ToleranceTableMatchesWhiteSheetUniformityRequirements()
    {
        Assert.Equal(3.0, MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.Full, MetamorfozeImagePlaneSize.UpToA3));
        Assert.Equal(4.0, MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.Full, MetamorfozeImagePlaneSize.UpToA2));
        Assert.Equal(5.0, MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.Full, MetamorfozeImagePlaneSize.UpToA1));
        Assert.Equal(6.0, MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.Full, MetamorfozeImagePlaneSize.UpToA0));
        Assert.Equal(3.0, MetamorfozeTolerances.WhiteBalanceDeltaEab(MetamorfozeQualityLevel.Full));

        Assert.Equal(5.0, MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.ExtraLight, MetamorfozeImagePlaneSize.UpToA3));
        Assert.Null(MetamorfozeTolerances.IlluminationDeltaL(MetamorfozeQualityLevel.ExtraLight, MetamorfozeImagePlaneSize.UpToA2));
        Assert.Equal(5.0, MetamorfozeTolerances.WhiteBalanceDeltaEab(MetamorfozeQualityLevel.ExtraLight));
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
}
