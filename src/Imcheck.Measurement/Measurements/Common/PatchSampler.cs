using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Common;

internal static class PatchSampler
{
    public static RgbPatchStatistics SampleCenteredSquare(
        Mat image,
        int channels,
        int sampleSize,
        double centerX,
        double centerY,
        bool rejectOutliers = false,
        double outlierSigmaThreshold = 3.0)
    {
        var rect = MeasurementGeometry.CenteredSquare(image.Width, image.Height, sampleSize, centerX, centerY);
        using var roi = new Mat(image, rect);
        var statistics = SampleRgb(roi, channels, rejectOutliers, outlierSigmaThreshold);
        return statistics with
        {
            CenterX = centerX,
            CenterY = centerY,
            X = rect.X,
            Y = rect.Y,
            Size = rect.Width
        };
    }

    public static RgbPatchStatistics SampleRgb(
        Mat roi,
        int channels,
        bool rejectOutliers = false,
        double outlierSigmaThreshold = 3.0)
    {
        if (channels == 1)
        {
            var (mean, noise) = MeasureChannel(roi, rejectOutliers, outlierSigmaThreshold);
            return new RgbPatchStatistics(mean, mean, mean, noise, noise, noise, IsColor: false, 0, 0, 0, 0, roi.Width);
        }

        Cv2.Split(roi, out var splitChannels);
        try
        {
            var (blueMean, blueNoise) = MeasureChannel(splitChannels[0], rejectOutliers, outlierSigmaThreshold);
            var (greenMean, greenNoise) = MeasureChannel(splitChannels[1], rejectOutliers, outlierSigmaThreshold);
            var (redMean, redNoise) = MeasureChannel(splitChannels[2], rejectOutliers, outlierSigmaThreshold);

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

    private static (double Mean, double Noise) MeasureChannel(Mat channel, bool rejectOutliers, double outlierSigmaThreshold)
    {
        if (!rejectOutliers)
        {
            return ImageStatistics.MeanAndPopulationStdDev(channel);
        }

        var clipped = ImageStatistics.MeanAndPopulationStdDevWithSigmaClipping(channel, outlierSigmaThreshold);
        return (clipped.Mean, clipped.StdDev);
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
