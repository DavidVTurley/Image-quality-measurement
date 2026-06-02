using OpenCvSharp;

namespace Imcheck.Measurement;

public sealed record GrayscaleNoiseOptions
{
    public bool Enabled { get; init; }

    public GrayscaleNoiseModel Model { get; init; } = GrayscaleNoiseModel.Gaussian;

    public double Amount { get; init; }

    public double Coverage { get; init; } = 0.75;

    public double VerticalGradient { get; init; }

    public int BlurRadius { get; init; }

    public double PatchBias { get; init; }

    public int Seed { get; init; } = 1234;
}

public enum GrayscaleNoiseModel
{
    Gaussian,
    Uniform,
    Patch
}

public static class GrayscaleNoiseApplicator
{
    public static void Apply(Mat image, IReadOnlyList<Rect> patchRects, GrayscaleNoiseOptions? options)
    {
        if (options is null || !options.Enabled || patchRects.Count == 0 ||
            (options.Amount <= 0 && options.VerticalGradient == 0 && options.BlurRadius <= 0 && options.PatchBias <= 0))
        {
            return;
        }

        var rng = new DeterministicRng(options.Seed <= 0 ? 1 : options.Seed);
        var patchOffsets = new double[patchRects.Count];
        var biasOffsets = new double[patchRects.Count];

        if (options.Model == GrayscaleNoiseModel.Patch)
        {
            for (var i = 0; i < patchOffsets.Length; i++)
            {
                patchOffsets[i] = SampleNoise(GrayscaleNoiseModel.Gaussian, options.Amount * options.Coverage, rng);
            }
        }

        if (options.PatchBias > 0)
        {
            for (var i = 0; i < biasOffsets.Length; i++)
            {
                biasOffsets[i] = SampleNoise(GrayscaleNoiseModel.Gaussian, options.PatchBias, rng);
            }
        }

        for (var patchIndex = 0; patchIndex < patchRects.Count; patchIndex++)
        {
            var rect = ClampRect(patchRects[patchIndex], image.Width, image.Height);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            for (var y = rect.Top; y < rect.Bottom; y++)
            {
                var relativeY = rect.Height > 1 ? (y - rect.Top) / (double)(rect.Height - 1) : 0.5;
                var gradientOffset = (relativeY - 0.5) * options.VerticalGradient;
                for (var x = rect.Left; x < rect.Right; x++)
                {
                    var offset = gradientOffset + biasOffsets[patchIndex];
                    if (options.Model == GrayscaleNoiseModel.Patch)
                    {
                        offset += patchOffsets[patchIndex];
                    }
                    else if (options.Amount > 0 && rng.NextDouble() < options.Coverage)
                    {
                        offset += SampleNoise(options.Model, options.Amount, rng);
                    }

                    var pixel = image.At<Vec3b>(y, x);
                    var value = ClampByte(pixel.Item0 + offset);
                    image.Set(y, x, new Vec3b(value, value, value));
                }
            }
        }

        if (options.BlurRadius > 0)
        {
            foreach (var patchRect in patchRects)
            {
                var rect = ClampRect(patchRect, image.Width, image.Height);
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    continue;
                }

                using var roi = image.SubMat(rect);
                Cv2.Blur(roi, roi, new Size(options.BlurRadius * 2 + 1, options.BlurRadius * 2 + 1));
            }
        }
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, width);
        var y = Math.Clamp(rect.Y, 0, height);
        var right = Math.Clamp(rect.Right, 0, width);
        var bottom = Math.Clamp(rect.Bottom, 0, height);
        return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private static byte ClampByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private static double SampleNoise(GrayscaleNoiseModel model, double amount, DeterministicRng rng)
    {
        if (amount <= 0)
        {
            return 0;
        }

        return model == GrayscaleNoiseModel.Uniform
            ? (rng.NextDouble() * 2 - 1) * amount
            : Gaussian(rng) * amount;
    }

    private static double Gaussian(DeterministicRng rng)
    {
        var u1 = Math.Max(rng.NextDouble(), 1e-12);
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private sealed class DeterministicRng(int seed)
    {
        private uint _state = (uint)seed;

        public double NextDouble()
        {
            _state += 0x6D2B79F5;
            var t = _state;
            t = (uint)Math.BigMul((int)(t ^ (t >> 15)), (int)(t | 1));
            t ^= t + (uint)Math.BigMul((int)(t ^ (t >> 7)), (int)(t | 61));
            return ((t ^ (t >> 14)) & 0xFFFFFFFFU) / 4294967296.0;
        }
    }
}
