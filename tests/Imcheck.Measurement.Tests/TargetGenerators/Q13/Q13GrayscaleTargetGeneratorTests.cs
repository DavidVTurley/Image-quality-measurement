using Imcheck.Measurement;
using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class Q13GrayscaleTargetGeneratorTests
{
    [Fact]
    public void GenerateCreatesExpectedSixHundredDpiPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new Q13GrayscaleTargetGenerator().Generate(path);

            Assert.Equal(path, result.OutputPath);
            Assert.Equal(4795, result.Width);
            Assert.Equal(709, result.Height);
            Assert.Equal(600, result.Dpi);

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            Assert.False(image.Empty());
            Assert.Equal(result.Width, image.Width);
            Assert.Equal(result.Height, image.Height);
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
    public void GeneratedPatchPixelsMatchDensityModelValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new Q13GrayscaleTargetGenerator().Generate(path);

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            for (var i = 0; i < Q13GrayscaleTargetGenerator.Patches.Count; i++)
            {
                var patch = Q13GrayscaleTargetGenerator.Patches[i];
                var layout = Q13GrayscaleTargetGenerator.GetPatchLayout(i, result.Width, result.Height);
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
    public void SharedNoiseChangesPatchPixelsDeterministically()
    {
        var cleanPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        var noisyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var generator = new Q13GrayscaleTargetGenerator();
            var clean = generator.Generate(cleanPath);
            var noisy = generator.Generate(noisyPath, new Q13GrayscaleTargetGeneratorOptions
            {
                Noise = new GrayscaleNoiseOptions
                {
                    Enabled = true,
                    Model = GrayscaleNoiseModel.Patch,
                    Amount = 12,
                    Coverage = 1,
                    Seed = 42
                }
            });

            Assert.Equal(clean.Width, noisy.Width);
            Assert.Equal(clean.Height, noisy.Height);

            using var cleanImage = Cv2.ImRead(cleanPath, ImreadModes.Color);
            using var noisyImage = Cv2.ImRead(noisyPath, ImreadModes.Color);
            var layout = Q13GrayscaleTargetGenerator.GetPatchLayout(5, clean.Width, clean.Height);

            Assert.NotEqual(
                cleanImage.At<Vec3b>(layout.CenterY, layout.CenterX),
                noisyImage.At<Vec3b>(layout.CenterY, layout.CenterX));
        }
        finally
        {
            if (File.Exists(cleanPath)) File.Delete(cleanPath);
            if (File.Exists(noisyPath)) File.Delete(noisyPath);
        }
    }
}
