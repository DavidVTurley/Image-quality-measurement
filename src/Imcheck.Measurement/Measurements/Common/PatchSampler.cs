using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Common;

internal static class PatchSampler
{
    public static RgbPatchStatistics SampleCenteredSquare(
        Mat image,
        int channels,
        int sampleSize,
        double centerX,
        double centerY)
    {
        var rect = MeasurementGeometry.CenteredSquare(image.Width, image.Height, sampleSize, centerX, centerY);
        using var roi = new Mat(image, rect);
        var statistics = SampleRgb(roi, channels);
        return statistics with
        {
            CenterX = centerX,
            CenterY = centerY,
            X = rect.X,
            Y = rect.Y,
            Size = rect.Width
        };
    }

    public static RgbPatchStatistics SampleRgb(Mat roi, int channels)
    {
        if (channels == 1)
        {
            var (mean, noise) = ImageStatistics.MeanAndPopulationStdDev(roi);
            return new RgbPatchStatistics(mean, mean, mean, noise, noise, noise, IsColor: false, 0, 0, 0, 0, roi.Width);
        }

        Cv2.Split(roi, out var splitChannels);
        try
        {
            var (blueMean, blueNoise) = ImageStatistics.MeanAndPopulationStdDev(splitChannels[0]);
            var (greenMean, greenNoise) = ImageStatistics.MeanAndPopulationStdDev(splitChannels[1]);
            var (redMean, redNoise) = ImageStatistics.MeanAndPopulationStdDev(splitChannels[2]);

            return new RgbPatchStatistics(redMean, greenMean, blueMean, redNoise, greenNoise, blueNoise, IsColor: true, 0, 0, 0, 0, roi.Width);
        }
        finally
        {
            foreach (var channel in splitChannels)
            {
                channel.Dispose();
            }
        }
    }
}

internal sealed record RgbPatchStatistics(
    double RedMean,
    double GreenMean,
    double BlueMean,
    double RedNoise,
    double GreenNoise,
    double BlueNoise,
    bool IsColor,
    double CenterX,
    double CenterY,
    int X,
    int Y,
    int Size);
