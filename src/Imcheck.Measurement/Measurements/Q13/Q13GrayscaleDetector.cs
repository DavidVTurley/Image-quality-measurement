using Imcheck.Measurement.Measurements.Common;
using OpenCvSharp;

namespace Imcheck.Measurement.Measurements.Q13;

public sealed class Q13GrayscaleDetector
{
    private const int PatchCount = 20;
    private const int MaximumDetectionDimension = 1400;
    private const int CanonicalStripWidth = PatchCount * 24;
    private const int CanonicalStripHeight = 48;

    public Q13DetectionResult Detect(string imagePath)
    {
        return Detect(imagePath, options: null);
    }

    public Q13DetectionResult Detect(string imagePath, Q13DetectionOptions? options)
    {
        using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        return Detect(image, options);
    }

    public Q13DetectionResult Detect(Mat image)
    {
        return Detect(image, options: null);
    }

    public Q13DetectionResult Detect(Mat image, Q13DetectionOptions? options)
    {
        if (image.Empty())
        {
            return Q13DetectionResult.NotFound;
        }

        using var originalBgr = ToBgr(image);
        using var bgr = CreateDetectionImage(originalBgr, out var detectionScale);
        using var gray = new Mat();
        ShowDebugImage(options, "01 input bgr", bgr);
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        ShowDebugImage(options, "02 grayscale", gray);
        Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
        ShowDebugImage(options, "03 blurred grayscale", gray);

        var candidates = new List<Q13StripGeometry>();
        CollectHorizontalRampCandidates(bgr, candidates);
        ShowDebugGeometryOverlay(options, "04 grayscale ramp candidates", bgr, candidates, null);

        var bestScore = 0.0;
        Q13StripGeometry? bestGeometry = null;
        foreach (var candidate in candidates)
        {
            EvaluateGeometryVariants(bgr, candidate, ref bestScore, ref bestGeometry);
        }

        if (bestGeometry is null || bestScore < 0.45)
        {
            ShowDebugGeometryOverlay(options, "05 final not found", bgr, candidates, null);
            return Q13DetectionResult.NotFound;
        }

        ShowDebugGeometryOverlay(options, "05 final best", bgr, candidates, bestGeometry);
        return new Q13DetectionResult(true, ScaleGeometry(bestGeometry, 1.0 / detectionScale), bestScore);
    }

    private static void CollectHorizontalRampCandidates(Mat image, List<Q13StripGeometry> candidates)
    {
        var axisAlignedScore = CollectAxisAlignedHorizontalRampCandidates(image, candidates);
        if (axisAlignedScore >= 0.90)
        {
            return;
        }

        foreach (var angleDegrees in new[] { -20.0, 20.0 })
        {
            using var rotation = Cv2.GetRotationMatrix2D(new Point2f(image.Width / 2.0f, image.Height / 2.0f), angleDegrees, 1.0);
            using var rotated = new Mat();
            Cv2.WarpAffine(image, rotated, rotation, image.Size(), InterpolationFlags.Area, BorderTypes.Replicate);

            var rotatedCandidates = new List<Q13StripGeometry>();
            CollectAxisAlignedHorizontalRampCandidates(rotated, rotatedCandidates);
            using var inverseRotation = new Mat();
            Cv2.InvertAffineTransform(rotation, inverseRotation);

            foreach (var candidate in rotatedCandidates)
            {
                AddCandidate(candidates, TransformGeometry(candidate, inverseRotation));
            }
        }
    }

    private static double CollectAxisAlignedHorizontalRampCandidates(Mat image, List<Q13StripGeometry> candidates)
    {
        using var gray = new Mat();
        using var integral = new Mat();
        using var squareIntegral = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Integral(gray, integral, squareIntegral, MatType.CV_64F);

        var best = new List<HorizontalRampCandidate>();
        for (var patchWidth = 14; patchWidth <= 84; patchWidth += 2)
        {
            var width = patchWidth * PatchCount;
            if (width > image.Width)
            {
                continue;
            }

            var sampleSize = Math.Max(3, MakeOdd((int)Math.Round(patchWidth * 0.45)));
            var height = Math.Max(sampleSize * 2, (int)Math.Round(patchWidth * 1.25));
            var xStep = Math.Max(3, patchWidth / 5);
            var yStep = Math.Max(3, patchWidth / 6);
            for (var y = height / 2; y < image.Height - height / 2; y += yStep)
            {
                for (var x = 0; x <= image.Width - width; x += xStep)
                {
                    var score = ScoreHorizontalRamp(integral, squareIntegral, image.Width, image.Height, x, y, patchWidth, sampleSize);
                    if (score < 0.82)
                    {
                        continue;
                    }

                    var centeredY = FindRampBandCenter(integral, squareIntegral, image.Width, image.Height, x, y, patchWidth, sampleSize, height, yStep);
                    score = ScoreHorizontalRamp(integral, squareIntegral, image.Width, image.Height, x, centeredY, patchWidth, sampleSize);
                    AddRampCandidate(best, new HorizontalRampCandidate(score, x, centeredY, width, height));
                }
            }
        }

        foreach (var candidate in best
                     .OrderByDescending(candidate => candidate.Score)
                     .ThenByDescending(candidate => candidate.Width)
                     .Take(96))
        {
            AddCandidate(candidates, new Q13StripGeometry(
                new Q13Point(candidate.X, candidate.Y - candidate.Height / 2.0),
                new Q13Point(candidate.X + candidate.Width, candidate.Y - candidate.Height / 2.0),
                new Q13Point(candidate.X + candidate.Width, candidate.Y + candidate.Height / 2.0)));
        }

        return best.Count == 0 ? 0 : best.Max(candidate => candidate.Score);
    }

    private static double ScoreHorizontalRamp(Mat integral, Mat squareIntegral, int imageWidth, int imageHeight, int x, int y, int patchWidth, int sampleSize)
    {
        var means = new double[PatchCount];
        var averageStdDev = 0.0;
        for (var index = 0; index < PatchCount; index++)
        {
            var centerX = x + (index + 0.5) * patchWidth;
            var sampleHeight = Math.Max(sampleSize, (int)Math.Round(patchWidth * 1.1));
            var rect = CenteredRectangle(imageWidth, imageHeight, sampleSize, sampleHeight, centerX, y);
            means[index] = MeanFromIntegral(integral, rect);
            averageStdDev += StandardDeviationFromIntegrals(integral, squareIntegral, rect);
        }

        averageStdDev /= PatchCount;

        var increasing = 0;
        var decreasing = 0;
        for (var index = 1; index < PatchCount; index++)
        {
            if (means[index] >= means[index - 1])
            {
                increasing++;
            }
            else
            {
                decreasing++;
            }
        }

        var direction = increasing >= decreasing ? 1.0 : -1.0;
        var brightEnd = direction < 0 ? means[0] : means[^1];
        var darkEnd = direction < 0 ? means[^1] : means[0];
        var monotonicScore = Math.Max(increasing, decreasing) / (double)(PatchCount - 1);
        var contrastScore = Math.Min(1.0, (means.Max() - means.Min()) / 120.0);
        var stepScore = StepSpacingScore(means, direction);
        var realStepScore = RealStepScore(means, direction);
        var brightScore = Math.Clamp((brightEnd - 190.0) / 45.0, 0, 1);
        var darkScore = Math.Clamp((120.0 - darkEnd) / 90.0, 0, 1);
        var uniformityScore = Math.Clamp(1.0 - averageStdDev / 22.0, 0, 1);

        return monotonicScore * 0.20 +
               contrastScore * 0.14 +
               stepScore * 0.18 +
               realStepScore * 0.14 +
               brightScore * 0.08 +
               darkScore * 0.16 +
               uniformityScore * 0.10;
    }

    private static int FindRampBandCenter(Mat integral, Mat squareIntegral, int imageWidth, int imageHeight, int x, int y, int patchWidth, int sampleSize, int height, int yStep)
    {
        var minimumY = height / 2;
        var maximumY = imageHeight - height / 2 - 1;
        var top = y;
        while (top - yStep >= minimumY && ScoreHorizontalRamp(integral, squareIntegral, imageWidth, imageHeight, x, top - yStep, patchWidth, sampleSize) >= 0.82)
        {
            top -= yStep;
        }

        var bottom = y;
        while (bottom + yStep <= maximumY && ScoreHorizontalRamp(integral, squareIntegral, imageWidth, imageHeight, x, bottom + yStep, patchWidth, sampleSize) >= 0.82)
        {
            bottom += yStep;
        }

        return (top + bottom) / 2;
    }

    private static void AddRampCandidate(List<HorizontalRampCandidate> candidates, HorizontalRampCandidate candidate)
    {
        var existingIndex = candidates.FindIndex(existing =>
                Math.Abs(existing.X - candidate.X) <= Math.Max(8, candidate.Width / PatchCount / 2) &&
                Math.Abs(existing.Y - candidate.Y) <= Math.Max(8, candidate.Height / 3) &&
                Math.Abs(existing.Width - candidate.Width) <= Math.Max(12, candidate.Width / PatchCount));
        if (existingIndex >= 0)
        {
            if (candidate.Score > candidates[existingIndex].Score)
            {
                candidates[existingIndex] = candidate;
            }

            return;
        }

        candidates.Add(candidate);
    }

    private static void EvaluateGeometryVariants(Mat image, Q13StripGeometry geometry, ref double bestScore, ref Q13StripGeometry? bestGeometry)
    {
        EvaluateGeometryVariant(image, geometry, 0, ref bestScore, ref bestGeometry);
    }

    private static void EvaluateGeometryVariant(Mat image, Q13StripGeometry geometry, double inferredPatches, ref double bestScore, ref Q13StripGeometry? bestGeometry)
    {
        var score = ScoreCandidate(image, geometry, inferredPatches);
        if (score > bestScore || IsBetterTieBreak(score, geometry, bestScore, bestGeometry))
        {
            bestScore = score;
            bestGeometry = geometry;
        }
    }

    private static bool IsBetterTieBreak(double score, Q13StripGeometry geometry, double bestScore, Q13StripGeometry? bestGeometry)
    {
        return bestGeometry is not null &&
               score >= bestScore - 0.035 &&
               geometry.Width > bestGeometry.Width * 1.08;
    }

    private static double ScoreCandidate(Mat image, Q13StripGeometry geometry, double inferredBrightPatches)
    {
        var stripWidth = CanonicalStripWidth;
        var stripHeight = CanonicalStripHeight;
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
        Cv2.WarpPerspective(image, warped, transform, new Size(stripWidth, stripHeight), InterpolationFlags.Area, BorderTypes.Replicate);

        var sampleSize = Math.Max(5, Math.Min(stripHeight / 3, stripWidth / PatchCount / 2));
        var sampleWidth = Math.Max(5, (int)Math.Round(stripWidth / (double)PatchCount * 0.45));
        var sampleHeight = Math.Max(sampleSize, (int)Math.Round(stripHeight * 0.65));
        var means = new double[PatchCount];
        var saturationPenalty = 0.0;
        var uniformityPenalty = 0.0;
        var sampleInsideCount = 0;
        var brightChromaPenalty = 0.0;
        for (var index = 0; index < PatchCount; index++)
        {
            var centerX = (index + 0.5) * stripWidth / PatchCount;
            var centerY = stripHeight / 2.0;
            var rect = CenteredRectangle(stripWidth, stripHeight, sampleWidth, sampleHeight, centerX, centerY);
            using var roi = new Mat(warped, rect);
            var mean = Cv2.Mean(roi);
            Cv2.MeanStdDev(roi, out _, out var stddev);
            var luminance = (mean.Val2 + mean.Val1 + mean.Val0) / 3.0;
            means[index] = luminance;
            saturationPenalty += Math.Abs(mean.Val2 - mean.Val1) + Math.Abs(mean.Val1 - mean.Val0) + Math.Abs(mean.Val2 - mean.Val0);
            uniformityPenalty += (stddev.Val0 + stddev.Val1 + stddev.Val2) / 3.0;
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
        var uniformityScore = Math.Clamp(1.0 - uniformityPenalty / PatchCount / 22.0, 0, 1);
        var stepScore = StepSpacingScore(means, monotonicDirection);
        var realStepScore = RealStepScore(means, monotonicDirection);
        var brightEndIndex = monotonicDirection < 0 ? 0 : PatchCount - 1;
        var darkEndIndex = monotonicDirection < 0 ? PatchCount - 1 : 0;
        var brightEndScore = BrightEndScore(geometry, brightEndIndex, means[brightEndIndex], brightChromaPenalty / 2.0, image.Width, image.Height);
        var darkContrastScore = Math.Clamp((means[brightEndIndex] - means[darkEndIndex]) / 120.0, 0, 1);
        var absoluteDarkScore = Math.Clamp((120.0 - means[darkEndIndex]) / 90.0, 0, 1);
        var insideScore = sampleInsideCount / (double)PatchCount;
        var inferencePenalty = inferredBrightPatches <= 1 ? 1.0 : 0.88;

        var score =
            monotonicScore * 0.22 +
            contrastScore * 0.13 +
            saturationScore * 0.08 +
            uniformityScore * 0.10 +
            stepScore * 0.13 +
            realStepScore * 0.10 +
            brightEndScore * 0.07 +
            darkContrastScore * 0.06 +
            absoluteDarkScore * 0.07 +
            insideScore * 0.04;
        return score * inferencePenalty;
    }

    private static Mat CreateDetectionImage(Mat image, out double detectionScale)
    {
        var maximumDimension = Math.Max(image.Width, image.Height);
        if (maximumDimension <= MaximumDetectionDimension)
        {
            detectionScale = 1.0;
            return image.Clone();
        }

        detectionScale = MaximumDetectionDimension / (double)maximumDimension;
        var resized = new Mat();
        Cv2.Resize(
            image,
            resized,
            new Size(
                Math.Max(1, (int)Math.Round(image.Width * detectionScale)),
                Math.Max(1, (int)Math.Round(image.Height * detectionScale))),
            0,
            0,
            InterpolationFlags.Area);
        return resized;
    }

    private static int MakeOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }

    private static double MeanFromIntegral(Mat integral, Rect rect)
    {
        var x0 = rect.X;
        var y0 = rect.Y;
        var x1 = rect.X + rect.Width;
        var y1 = rect.Y + rect.Height;
        var sum = integral.At<double>(y1, x1) -
                  integral.At<double>(y0, x1) -
                  integral.At<double>(y1, x0) +
                  integral.At<double>(y0, x0);
        return sum / Math.Max(1, rect.Width * rect.Height);
    }

    private static Rect CenteredRectangle(int imageWidth, int imageHeight, int width, int height, double centerX, double centerY)
    {
        width = Math.Clamp(width, 1, imageWidth);
        height = Math.Clamp(height, 1, imageHeight);
        var x = (int)Math.Round(centerX - width / 2.0);
        var y = (int)Math.Round(centerY - height / 2.0);
        x = Math.Clamp(x, 0, Math.Max(0, imageWidth - width));
        y = Math.Clamp(y, 0, Math.Max(0, imageHeight - height));
        return new Rect(x, y, width, height);
    }

    private static double StandardDeviationFromIntegrals(Mat integral, Mat squareIntegral, Rect rect)
    {
        var area = Math.Max(1, rect.Width * rect.Height);
        var mean = MeanFromIntegral(integral, rect);
        var squareMean = MeanFromIntegral(squareIntegral, rect);
        return Math.Sqrt(Math.Max(0, squareMean - mean * mean));
    }

    private static void AddCandidate(List<Q13StripGeometry> candidates, Q13StripGeometry geometry)
    {
        if (geometry.Width < PatchCount * 4 || geometry.Height < 4)
        {
            return;
        }

        if (candidates.Any(candidate => IsSimilar(candidate, geometry)))
        {
            return;
        }

        candidates.Add(geometry);
    }

    private static bool IsSimilar(Q13StripGeometry first, Q13StripGeometry second)
    {
        var centerTolerance = Math.Max(6, Math.Max(first.Height, second.Height) * 0.35);
        var widthTolerance = Math.Max(8, Math.Max(first.Width, second.Width) * 0.08);
        var heightTolerance = Math.Max(4, Math.Max(first.Height, second.Height) * 0.20);
        return Distance(first.Center, second.Center) <= centerTolerance &&
               Math.Abs(first.Width - second.Width) <= widthTolerance &&
               Math.Abs(first.Height - second.Height) <= heightTolerance &&
               AngleDifference(first.AngleRadians, second.AngleRadians) <= Math.PI / 36.0;
    }

    private static double AngleDifference(double first, double second)
    {
        var difference = Math.Abs(first - second) % Math.PI;
        return Math.Min(difference, Math.PI - difference);
    }

    private static Q13StripGeometry ScaleGeometry(Q13StripGeometry geometry, double scale)
    {
        return new Q13StripGeometry(
            new Q13Point(geometry.TopLeft.X * scale, geometry.TopLeft.Y * scale),
            new Q13Point(geometry.TopRight.X * scale, geometry.TopRight.Y * scale),
            new Q13Point(geometry.BottomRight.X * scale, geometry.BottomRight.Y * scale));
    }

    private static Q13StripGeometry TransformGeometry(Q13StripGeometry geometry, Mat affineTransform)
    {
        return new Q13StripGeometry(
            TransformPoint(geometry.TopLeft, affineTransform),
            TransformPoint(geometry.TopRight, affineTransform),
            TransformPoint(geometry.BottomRight, affineTransform));
    }

    private static Q13Point TransformPoint(Q13Point point, Mat affineTransform)
    {
        return new Q13Point(
            affineTransform.At<double>(0, 0) * point.X + affineTransform.At<double>(0, 1) * point.Y + affineTransform.At<double>(0, 2),
            affineTransform.At<double>(1, 0) * point.X + affineTransform.At<double>(1, 1) * point.Y + affineTransform.At<double>(1, 2));
    }

    private static Mat DrawGeometryOverlay(Mat image, IReadOnlyList<Q13StripGeometry> candidates, Q13StripGeometry? bestGeometry)
    {
        var overlay = image.Clone();
        foreach (var geometry in candidates)
        {
            DrawGeometry(overlay, geometry, new Scalar(0, 180, 255), thickness: 2);
        }

        if (bestGeometry is not null)
        {
            DrawGeometry(overlay, bestGeometry, new Scalar(0, 0, 255), thickness: 4);
            for (var index = 0; index < PatchCount; index++)
            {
                var center = bestGeometry.PointAt((index + 0.5) / PatchCount, 0.5);
                Cv2.Circle(overlay, ToPoint(center), 4, new Scalar(0, 255, 255), -1, LineTypes.AntiAlias);
            }
        }

        return overlay;
    }

    private static void ShowDebugGeometryOverlay(
        Q13DetectionOptions? options,
        string stepName,
        Mat image,
        IReadOnlyList<Q13StripGeometry> candidates,
        Q13StripGeometry? bestGeometry)
    {
        if (options?.ShowDebugImages != true)
        {
            return;
        }

        using var overlay = DrawGeometryOverlay(image, candidates, bestGeometry);
        ShowDebugImage(options, stepName, overlay);
    }

    private static void DrawGeometry(Mat image, Q13StripGeometry geometry, Scalar color, int thickness)
    {
        var points = new[]
        {
            ToPoint(geometry.TopLeft),
            ToPoint(geometry.TopRight),
            ToPoint(geometry.BottomRight),
            ToPoint(geometry.BottomLeft)
        };
        Cv2.Polylines(image, [points], true, color, thickness, LineTypes.AntiAlias);
    }

    private static Point ToPoint(Q13Point point)
    {
        return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }

    private static void ShowDebugImage(Q13DetectionOptions? options, string stepName, Mat image)
    {
        if (options?.ShowDebugImages != true)
        {
            return;
        }

        using var display = CreateDebugDisplayImage(image, options.MaximumDebugImageDimension);
        Cv2.ImShow($"{options.DebugWindowPrefix} {stepName}", display);
        Cv2.WaitKey(options.DebugWaitMilliseconds);
    }

    private static Mat CreateDebugDisplayImage(Mat image, int maximumDimension)
    {
        if (image.Channels() == 1)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(image, bgr, ColorConversionCodes.GRAY2BGR);
            return ResizeForDebugDisplay(bgr, maximumDimension);
        }

        return ResizeForDebugDisplay(image, maximumDimension);
    }

    private static Mat ResizeForDebugDisplay(Mat image, int maximumDimension)
    {
        var largestDimension = Math.Max(image.Width, image.Height);
        if (largestDimension <= maximumDimension)
        {
            return image.Clone();
        }

        var scale = maximumDimension / (double)largestDimension;
        var resized = new Mat();
        Cv2.Resize(
            image,
            resized,
            new Size(
                Math.Max(1, (int)Math.Round(image.Width * scale)),
                Math.Max(1, (int)Math.Round(image.Height * scale))),
            0,
            0,
            InterpolationFlags.Area);
        return resized;
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

    private static double RealStepScore(IReadOnlyList<double> means, double direction)
    {
        var meaningfulSteps = 0;
        for (var i = 1; i < means.Count; i++)
        {
            if ((means[i] - means[i - 1]) * direction >= 4.0)
            {
                meaningfulSteps++;
            }
        }

        return meaningfulSteps / (double)(means.Count - 1);
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

    private sealed record HorizontalRampCandidate(double Score, int X, int Y, int Width, int Height);
}
