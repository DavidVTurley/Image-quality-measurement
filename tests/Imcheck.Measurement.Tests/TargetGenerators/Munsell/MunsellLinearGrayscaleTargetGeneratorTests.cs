using Imcheck.Measurement;
using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class MunsellLinearGrayscaleTargetGeneratorTests
{
    [Fact]
    public void GenerateCreatesExpectedSixHundredDpiPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new MunsellLinearGrayscaleTargetGenerator().Generate(path);

            Assert.Equal(path, result.OutputPath);
            Assert.Equal(6024, result.Width);
            Assert.Equal(756, result.Height);
            Assert.Equal(600, result.Dpi);

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            Assert.False(image.Empty());
            Assert.Equal(6024, image.Width);
            Assert.Equal(756, image.Height);
            Assert.Equal(MatType.CV_8UC3, image.Type());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void GeneratedPatchPixelsMatchTheoreticalNeutralValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new MunsellLinearGrayscaleTargetGenerator().Generate(path);

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            for (var i = 0; i < MunsellLinearGrayscaleTargetGenerator.Patches.Count; i++)
            {
                var patch = MunsellLinearGrayscaleTargetGenerator.Patches[i];
                var layout = MunsellLinearGrayscaleTargetGenerator.GetPatchLayout(i, result.Width, result.Height);
                var actual = image.At<Vec3b>(layout.CenterY, layout.CenterX);
                var expected = new Vec3b(patch.EncodedRgb, patch.EncodedRgb, patch.EncodedRgb);

                Assert.Equal(expected, actual);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LayoutTogglesDoNotChangePatchValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new MunsellLinearGrayscaleTargetGenerator().Generate(
                path,
                new MunsellLinearGrayscaleTargetGeneratorOptions
                {
                    ShowLabels = false,
                    ShowMillimeterScale = false,
                    ShowTitle = false
                });

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            for (var i = 0; i < MunsellLinearGrayscaleTargetGenerator.Patches.Count; i++)
            {
                var patch = MunsellLinearGrayscaleTargetGenerator.Patches[i];
                var layout = MunsellLinearGrayscaleTargetGenerator.GetPatchLayout(i, result.Width, result.Height);
                var actual = image.At<Vec3b>(layout.CenterY, layout.CenterX);
                var expected = new Vec3b(patch.EncodedRgb, patch.EncodedRgb, patch.EncodedRgb);

                Assert.Equal(expected, actual);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
