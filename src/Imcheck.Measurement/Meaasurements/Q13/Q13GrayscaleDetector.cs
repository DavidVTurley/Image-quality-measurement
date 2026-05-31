using Imcheck.Measurement.Meaasurements.Common;
using OpenCvSharp;

namespace Imcheck.Measurement.Meaasurements.Q13;

public sealed class Q13GrayscaleDetector
{
    private const int PatchCount = 20;
    private const double MinimumAspectRatio = 5.0;
    private const double MaximumAspectRatio = 30.0;

    public Q13DetectionResult Detect(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        return Detect(image);
    }

    public Q13DetectionResult Detect(Mat image)
    {
        if (image.Empty())
        {
            return Q13DetectionResult.NotFound;
        }

        using var bgr = ToBgr(image);
        using var gray = new Mat();
        using var edges = new Mat();
        using var closed = new Mat();
        using var mask = new Mat();
        using var connectedMask = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
        Cv2.Canny(gray, edges, 40, 120);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 3));
        Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel);
        Cv2.Threshold(gray, mask, 242, 255, ThresholdTypes.BinaryInv);
        using var wideKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(35, 7));
        Cv2.MorphologyEx(mask, connectedMask, MorphTypes.Close, wideKernel);

        var bestScore = 0.0;
        Q13StripGeometry? bestGeometry = null;
        EvaluateContours(bgr, closed, ref bestScore, ref bestGeometry);
        EvaluateContours(bgr, connectedMask, ref bestScore, ref bestGeometry);
        EvaluateMaskPointCloud(bgr, mask, ref bestScore, ref bestGeometry);

        return bestGeometry is null || bestScore < 0.45
            ? Q13DetectionResult.NotFound
            : new Q13DetectionResult(true, bestGeometry, bestScore);
    }

    private static void EvaluateContours(Mat image, Mat contourImage, ref double bestScore, ref Q13StripGeometry? bestGeometry)
    {
        Cv2.FindContours(contourImage, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 500)
            {
                continue;
            }

            var rect = Cv2.MinAreaRect(contour);
            var width = Math.Max(rect.Size.Width, rect.Size.Height);
            var height = Math.Min(rect.Size.Width, rect.Size.Height);
            if (height <= 0)
            {
                continue;
            }

            var aspect = (double)width / height;
            if (aspect is < MinimumAspectRatio or > MaximumAspectRatio)
            {
                continue;
            }

            EvaluateGeometryVariants(image, FromRotatedRect(rect), ref bestScore, ref bestGeometry);
        }
    }

    private static void EvaluateMaskPointCloud(Mat image, Mat mask, ref double bestScore, ref Q13StripGeometry? bestGeometry)
    {
        using var pointsMat = new Mat();
        Cv2.FindNonZero(mask, pointsMat);
        pointsMat.GetArray(out Point[] points);
        if (points is null || points.Length < 100)
        {
            return;
        }

        var rect = Cv2.MinAreaRect(points);
        var width = Math.Max(rect.Size.Width, rect.Size.Height);
        var height = Math.Min(rect.Size.Width, rect.Size.Height);
        if (height <= 0)
        {
            return;
        }

        var aspect = (double)width / height;
        if (aspect is < MinimumAspectRatio or > MaximumAspectRatio)
        {
            return;
        }

        EvaluateGeometryVariants(image, FromRotatedRect(rect), ref bestScore, ref bestGeometry);
    }

    private static void EvaluateGeometryVariants(Mat image, Q13StripGeometry geometry, ref double bestScore, ref Q13StripGeometry? bestGeometry)
    {
        var onePatch = 1.0 / (PatchCount - 1);
        var variants = new (Q13StripGeometry Geometry, double InferredBrightPatches)[]
        {
            (geometry, 0),
            (geometry.Extend(onePatch, 0), 1),
            (geometry.Extend(0, onePatch), 1),
            (geometry.Extend(onePatch * 2, 0), 2),
            (geometry.Extend(0, onePatch * 2), 2),
            (geometry.Extend(onePatch / 2.0, onePatch / 2.0), 1)
        };

        foreach (var variant in variants)
        {
            var score = ScoreCandidate(image, variant.Geometry, variant.InferredBrightPatches);
            if (score > bestScore)
            {
                bestScore = score;
                bestGeometry = variant.Geometry;
            }
        }
    }

    private static double ScoreCandidate(Mat image, Q13StripGeometry geometry, double inferredBrightPatches)
    {
        var stripWidth = Math.Max(PatchCount * 20, (int)Math.Round(geometry.Width));
        var stripHeight = Math.Max(20, (int)Math.Round(geometry.Height));
        if (stripWidth <= 0 || stripHeight <= 0)
        {
            return 0;
        }

        using var warped = new Mat();
        var destination = new[]
        {
            new Point2f(0, 0),
            new Point2f(stripWidth - 1, 0),
            new Point2f(stripWidth - 1, stripHeight - 1),
            new Point2f(0, stripHeight - 1)
        };

        using var transform = Cv2.GetPerspectiveTransform(geometry.SourcePoints(), destination);
        Cv2.WarpPerspective(image, warped, transform, new Size(stripWidth, stripHeight), InterpolationFlags.Linear, BorderTypes.Replicate);

        var sampleSize = Math.Max(5, Math.Min(stripHeight / 3, stripWidth / PatchCount / 2));
        var means = new double[PatchCount];
        var saturationPenalty = 0.0;
        var sampleInsideCount = 0;
        var brightChromaPenalty = 0.0;
        for (var index = 0; index < PatchCount; index++)
        {
            var centerX = (index + 0.5) * stripWidth / PatchCount;
            var centerY = stripHeight / 2.0;
            var rect = MeasurementGeometry.CenteredSquare(stripWidth, stripHeight, sampleSize, centerX, centerY);
            using var roi = new Mat(warped, rect);
            var mean = Cv2.Mean(roi);
            var luminance = (mean.Val2 + mean.Val1 + mean.Val0) / 3.0;
            means[index] = luminance;
            saturationPenalty += Math.Abs(mean.Val2 - mean.Val1) + Math.Abs(mean.Val1 - mean.Val0) + Math.Abs(mean.Val2 - mean.Val0);
            if (SampleCenterInsideImage(geometry, index, image.Width, image.Height))
            {
                sampleInsideCount++;
            }

            if (index is 0 or PatchCount - 1)
            {
                brightChromaPenalty += Math.Abs(mean.Val2 - mean.Val1) + Math.Abs(mean.Val1 - mean.Val0) + Math.Abs(mean.Val2 - mean.Val0);
            }
        }

        var increasing = 0;
        var decreasing = 0;
        for (var i = 1; i < means.Length; i++)
        {
            if (means[i] >= means[i - 1])
            {
                increasing++;
            }
            else
            {
                decreasing++;
            }
        }

        var monotonicDirection = increasing >= decreasing ? 1.0 : -1.0;
        var monotonicScore = Math.Max(increasing, decreasing) / (double)(PatchCount - 1);
        var contrastScore = Math.Min(1.0, (means.Max() - means.Min()) / 120.0);
        var saturationScore = Math.Max(0, 1.0 - saturationPenalty / PatchCount / 60.0);
        var stepScore = StepSpacingScore(means, monotonicDirection);
        var brightEndIndex = monotonicDirection < 0 ? 0 : PatchCount - 1;
        var darkEndIndex = monotonicDirection < 0 ? PatchCount - 1 : 0;
        var brightEndScore = BrightEndScore(geometry, brightEndIndex, means[brightEndIndex], brightChromaPenalty / 2.0, image.Width, image.Height);
        var darkEndScore = Math.Clamp((means[brightEndIndex] - means[darkEndIndex]) / 120.0, 0, 1);
        var insideScore = sampleInsideCount / (double)PatchCount;
        var inferencePenalty = inferredBrightPatches <= 1 ? 1.0 : 0.88;

        var score =
            monotonicScore * 0.32 +
            contrastScore * 0.18 +
            saturationScore * 0.12 +
            stepScore * 0.18 +
            brightEndScore * 0.10 +
            darkEndScore * 0.05 +
            insideScore * 0.05;
        return score * inferencePenalty;
    }

    private static double BrightEndScore(
        Q13StripGeometry geometry,
        int brightEndIndex,
        double brightMean,
        double brightChromaPenalty,
        int imageWidth,
        int imageHeight)
    {
        var center = geometry.PointAt((brightEndIndex + 0.5) / PatchCount, 0.5);
        var inside = center.X >= 0 && center.Y >= 0 && center.X < imageWidth && center.Y < imageHeight;
        var edgeFlush = IsFlushWithImageEdge(geometry, brightEndIndex, imageWidth, imageHeight);
        if (!inside && !edgeFlush)
        {
            return 0;
        }

        var luminanceScore = Math.Clamp((brightMean - 190.0) / 45.0, 0, 1);
        var chromaScore = Math.Clamp(1.0 - brightChromaPenalty / 45.0, 0, 1);
        var edgeScore = edgeFlush ? 1.0 : 0.85;
        return (luminanceScore * 0.55 + chromaScore * 0.30 + edgeScore * 0.15);
    }

    private static bool SampleCenterInsideImage(Q13StripGeometry geometry, int index, int imageWidth, int imageHeight)
    {
        var center = geometry.PointAt((index + 0.5) / PatchCount, 0.5);
        return center.X >= 0 && center.Y >= 0 && center.X < imageWidth && center.Y < imageHeight;
    }

    private static bool IsFlushWithImageEdge(Q13StripGeometry geometry, int brightEndIndex, int imageWidth, int imageHeight)
    {
        var edgeA = brightEndIndex == 0 ? geometry.PointAt(0, 0.5) : geometry.PointAt(1, 0.5);
        var tolerance = Math.Max(3, geometry.Height * 0.15);
        return edgeA.X <= tolerance ||
               edgeA.Y <= tolerance ||
               imageWidth - 1 - edgeA.X <= tolerance ||
               imageHeight - 1 - edgeA.Y <= tolerance;
    }

    private static double StepSpacingScore(IReadOnlyList<double> means, double direction)
    {
        var steps = new List<double>(means.Count - 1);
        for (var i = 1; i < means.Count; i++)
        {
            var step = (means[i] - means[i - 1]) * direction;
            if (step > 0)
            {
                steps.Add(step);
            }
        }

        if (steps.Count < means.Count - 2)
        {
            return 0;
        }

        var average = steps.Average();
        if (average <= 0)
        {
            return 0;
        }

        var meanAbsoluteError = steps.Average(step => Math.Abs(step - average));
        return Math.Clamp(1.0 - meanAbsoluteError / average, 0, 1);
    }

    private static Q13StripGeometry FromRotatedRect(RotatedRect rect)
    {
        var points = rect.Points().Select(point => new Q13Point(point.X, point.Y)).ToArray();
        var center = new Q13Point(points.Average(point => point.X), points.Average(point => point.Y));
        var ordered = points
            .Select(point => (Point: point, Angle: Math.Atan2(point.Y - center.Y, point.X - center.X)))
            .OrderBy(item => item.Angle)
            .Select(item => item.Point)
            .ToArray();

        var topCandidates = ordered.OrderBy(point => point.Y).Take(2).OrderBy(point => point.X).ToArray();
        var bottomCandidates = ordered.OrderByDescending(point => point.Y).Take(2).OrderBy(point => point.X).ToArray();
        var topLeft = topCandidates[0];
        var topRight = topCandidates[1];
        var bottomRight = bottomCandidates[1];

        if (Distance(topLeft, topRight) < Distance(topRight, bottomRight))
        {
            var leftCandidates = ordered.OrderBy(point => point.X).Take(2).OrderBy(point => point.Y).ToArray();
            var rightCandidates = ordered.OrderByDescending(point => point.X).Take(2).OrderBy(point => point.Y).ToArray();
            topLeft = leftCandidates[0];
            topRight = rightCandidates[0];
            bottomRight = rightCandidates[1];
        }

        return new Q13StripGeometry(topLeft, topRight, bottomRight);
    }

    private static Mat ToBgr(Mat image)
    {
        if (image.Channels() == 3)
        {
            return image.Clone();
        }

        var bgr = new Mat();
        if (image.Channels() == 1)
        {
            Cv2.CvtColor(image, bgr, ColorConversionCodes.GRAY2BGR);
            return bgr;
        }

        Cv2.CvtColor(image, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    private static double Distance(Q13Point first, Q13Point second)
    {
        return Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
    }
}
