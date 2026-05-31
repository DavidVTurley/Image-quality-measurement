using Imcheck.Measurement.Measurements.Q13;
using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class Q13GeometryTests
{
    [Fact]
    public void DefaultSampleRegionsCoverTwentyPatchCenters()
    {
        var regions = Q13StripGeometry.CreateDefaultSampleRegions();

        Assert.Equal(20, regions.Count);
        Assert.Equal(0, regions[0].PatchIndex);
        Assert.Equal(0.025, regions[0].CenterX, precision: 6);
        Assert.Equal(0.5, regions[0].CenterY, precision: 6);
        Assert.Equal(19, regions[19].PatchIndex);
        Assert.Equal(0.975, regions[19].CenterX, precision: 6);
    }

    [Fact]
    public void FromThreePointsKeepsStripRectangular()
    {
        var geometry = Q13StripGeometry.FromThreePoints(
            new Q13Point(10, 10),
            new Q13Point(110, 20),
            new Q13Point(125, 80));

        var top = new Q13Point(geometry.TopRight.X - geometry.TopLeft.X, geometry.TopRight.Y - geometry.TopLeft.Y);
        var right = new Q13Point(geometry.BottomRight.X - geometry.TopRight.X, geometry.BottomRight.Y - geometry.TopRight.Y);

        Assert.Equal(0, top.X * right.X + top.Y * right.Y, precision: 6);
    }

    [Fact]
    public void ResizeFromCornerKeepsStripRectangular()
    {
        var geometry = new Q13StripGeometry(
            new Q13Point(10, 10),
            new Q13Point(110, 10),
            new Q13Point(110, 40));

        var resized = geometry.ResizeFromCorner(Q13StripCorner.TopLeft, new Q13Point(0, 0));
        var top = new Q13Point(resized.TopRight.X - resized.TopLeft.X, resized.TopRight.Y - resized.TopLeft.Y);
        var right = new Q13Point(resized.BottomRight.X - resized.TopRight.X, resized.BottomRight.Y - resized.TopRight.Y);

        Assert.Equal(0, top.X * right.X + top.Y * right.Y, precision: 6);
        Assert.Equal(resized.BottomLeft.X + resized.TopRight.X - resized.TopLeft.X, resized.BottomRight.X, precision: 6);
    }

    [Fact]
    public void StripGeometryMeasurementSamplesRotatedSyntheticTarget()
    {
        using var image = CreateRotatedQ13Image(out var geometry, out var expected);
        var path = WriteTempImage(image);
        try
        {
            var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions
            {
                StripGeometry = geometry,
                SampleRegions = Q13StripGeometry.CreateDefaultSampleRegions(normalizedSampleSize: 0.18)
            });

            Assert.Equal(20, result.Patches.Count);
            Assert.All(result.Patches.Zip(expected), pair =>
                Assert.InRange(pair.First.Output, pair.Second - 2, pair.Second + 2));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectorFindsRotatedSyntheticTargetInLargerImage()
    {
        using var image = CreateRotatedQ13Image(out _, out _);

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.True(result.Found, $"Expected rotated Q13 target to be detected; score was {result.Score:0.###}.");
        Assert.NotNull(result.Geometry);
        Assert.True(result.Score > 0.45);
    }

    [Fact]
    public void DetectorIncludesWhitePatchInTightCrop()
    {
        const int patchWidth = 32;
        using var image = new Mat(70, patchWidth * 20, MatType.CV_8UC3, new Scalar(246, 246, 246));
        for (var index = 0; index < 20; index++)
        {
            var value = 246 - index * 10;
            Cv2.Rectangle(image, new Rect(index * patchWidth, 8, patchWidth, 54), new Scalar(value, value, value), -1);
        }

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.True(result.Found, $"Expected tight Q13 crop to be detected; score was {result.Score:0.###}.");
        Assert.NotNull(result.Geometry);
        var firstCenter = result.Geometry!.PointAt(0.025, 0.5);
        Assert.True(firstCenter.X < patchWidth, $"Expected first sample center in the first patch, got X={firstCenter.X:0.##}.");
    }

    [Fact]
    public void DetectorIncludesWhitePatchWhenDarkBodyStartsAtSecondPatch()
    {
        const int patchWidth = 32;
        using var image = new Mat(70, patchWidth * 20, MatType.CV_8UC3, new Scalar(246, 246, 246));
        for (var index = 1; index < 20; index++)
        {
            var value = 246 - index * 10;
            Cv2.Rectangle(image, new Rect(index * patchWidth, 8, patchWidth, 54), new Scalar(value, value, value), -1);
        }

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.True(result.Found, $"Expected 19-visible-patch Q13 crop to be detected; score was {result.Score:0.###}.");
        Assert.NotNull(result.Geometry);
        var firstCenter = result.Geometry!.PointAt(0.025, 0.5);
        Assert.True(firstCenter.X < patchWidth, $"Expected inferred first sample center in the white first patch, got X={firstCenter.X:0.##}.");
    }

    [Fact]
    public void DetectorFindsTightCropOnBlackBackground()
    {
        const int patchWidth = 32;
        using var image = new Mat(110, patchWidth * 20 + 80, MatType.CV_8UC3, new Scalar(0, 0, 0));
        for (var index = 0; index < 20; index++)
        {
            var value = 246 - index * 10;
            Cv2.Rectangle(image, new Rect(40 + index * patchWidth, 28, patchWidth, 54), new Scalar(value, value, value), -1);
        }

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.True(result.Found, $"Expected black-background Q13 crop to be detected; score was {result.Score:0.###}.");
        Assert.NotNull(result.Geometry);
        Assert.True(result.Geometry!.Width > patchWidth * 18);
    }

    [Fact]
    public void DetectorReturnsNotFoundWhenNoGrayscaleStripExists()
    {
        using var image = new Mat(700, 1000, MatType.CV_8UC3, new Scalar(245, 245, 245));
        Cv2.Rectangle(image, new Rect(100, 100, 160, 120), new Scalar(40, 130, 220), -1);

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.False(result.Found);
        Assert.Null(result.Geometry);
    }

    [Fact]
    public void DetectorRejectsWhiteBackgroundWithSingleDarkObject()
    {
        using var image = new Mat(180, 680, MatType.CV_8UC3, new Scalar(248, 248, 248));
        Cv2.Rectangle(image, new Rect(220, 60, 180, 48), new Scalar(35, 35, 35), -1);

        var result = new Q13GrayscaleDetector().Detect(image);

        Assert.False(result.Found);
        Assert.Null(result.Geometry);
    }

    private static Mat CreateRotatedQ13Image(out Q13StripGeometry geometry, out int[] expected)
    {
        var image = new Mat(700, 1000, MatType.CV_8UC3, new Scalar(245, 245, 245));
        var baseGeometry = new Q13StripGeometry(
            new Q13Point(260, 290),
            new Q13Point(760, 290),
            new Q13Point(760, 350));
        geometry = baseGeometry.Rotate(Math.PI / 9.0);
        expected = Enumerable.Range(0, 20).Select(index => 240 - index * 10).ToArray();

        for (var index = 0; index < 20; index++)
        {
            var x0 = index / 20.0;
            var x1 = (index + 1) / 20.0;
            var points = new[]
            {
                ToPoint(geometry.PointAt(x0, 0)),
                ToPoint(geometry.PointAt(x1, 0)),
                ToPoint(geometry.PointAt(x1, 1)),
                ToPoint(geometry.PointAt(x0, 1))
            };
            var value = expected[index];
            Cv2.FillConvexPoly(image, points, new Scalar(value, value, value), LineTypes.AntiAlias);
        }

        return image;
    }

    private static Point ToPoint(Q13Point point)
    {
        return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }

    private static string WriteTempImage(Mat image)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, image);
        return path;
    }
}
