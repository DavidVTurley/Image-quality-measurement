using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Common;

internal static class ImageValidation
{
    public static Mat LoadRequired(string imagePath, ImreadModes mode, string operationName)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        var image = Cv2.ImRead(imagePath, mode);
        if (image.Empty())
        {
            image.Dispose();
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        return image;
    }

    public static int RequireChannels(Mat image)
    {
        var channels = image.Channels();
        if (channels is not (1 or 3 or 4))
        {
            throw new NotSupportedException($"Unsupported channel count: {channels}.");
        }

        return channels;
    }

    public static int RequireEightBitChannels(Mat image, string implementationName)
    {
        if (image.Depth() != MatType.CV_8U)
        {
            throw new NotSupportedException($"Only 8-bit images are supported in the {implementationName} implementation.");
        }

        return RequireChannels(image);
    }

    public static int RequireEightOrSixteenBitChannels(Mat image, string implementationName)
    {
        if (image.Depth() is not (MatType.CV_8U or MatType.CV_16U))
        {
            throw new NotSupportedException($"Only 8-bit and 16-bit images are supported for {implementationName}.");
        }

        return RequireChannels(image);
    }
}
