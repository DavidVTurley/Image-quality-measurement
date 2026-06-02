using Imcheck.Measurement.Measurements.Q13;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IOPath = System.IO.Path;

namespace Imcheck.App;

public partial class MainWindow
{
    private readonly List<Q13Point> _q13ManualPoints = [];
    private Q13PlacementDragKind _q13DragKind = Q13PlacementDragKind.None;
    private int _q13DragSampleIndex = -1;
    private Q13Point? _q13LastDragImagePoint;

    private void Q13OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Q-13 image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            OpenQ13Image(dialog.FileName, autoLoadNeighborCsv: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 placement failed.";
            MessageBox.Show(this, ex.Message, "Placement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13OpenImageCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var imageDialog = new OpenFileDialog
        {
            Title = "Open Q-13 image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (imageDialog.ShowDialog(this) != true)
        {
            return;
        }

        var csvDialog = new OpenFileDialog
        {
            Title = "Open Q-13 results CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            InitialDirectory = IOPath.GetDirectoryName(imageDialog.FileName)
        };

        if (csvDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            MeasureQ13FromResultsCsv(imageDialog.FileName, csvDialog.FileName, "Imported Q13 image and results CSV.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 results CSV import failed.";
            MessageBox.Show(this, ex.Message, "Could not load Q13 results CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13ClearPointsButton_Click(object sender, RoutedEventArgs e)
    {
        var imagePath = _currentQ13Result?.ImagePath ?? _pendingQ13ImagePath;
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        _q13SampleCenters = null;
        Q13ClearPointsButton.IsEnabled = false;
        _acceptedQ13Geometry = null;
        _acceptedQ13Regions = null;
        StatusText.Text = "Reselecting Q13 measurement area...";
        BeginQ13Placement(imagePath);
    }

    private void Q13PreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawQ13Overlay();
    }

    private void Q13PlacementHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawQ13PlacementOverlay();
    }

    private void Q13PlacementHost_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _pendingQ13Geometry is not null)
        {
            AcceptQ13Placement();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelQ13Placement();
            e.Handled = true;
        }
    }

    private void Q13PlacementHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Q13PlacementHost.Focus();
        var imagePoint = Q13PlacementImagePoint(e.GetPosition(Q13PlacementHost));
        if (imagePoint is null)
        {
            return;
        }

        if (_pendingQ13Geometry is null)
        {
            _q13ManualPoints.Add(imagePoint);
            if (_q13ManualPoints.Count == 3)
            {
                _pendingQ13Geometry = Q13StripGeometry.FromThreePoints(_q13ManualPoints[0], _q13ManualPoints[1], _q13ManualPoints[2]);
                _pendingQ13Regions = Q13StripGeometry.CreateDefaultSampleRegions().ToList();
                Q13AcceptPlacementButton.IsEnabled = true;
                Q13PlacementStatusText.Text = "Manual Q13 strip defined. Drag the strip or red squares, then press Enter or Accept.";
            }
            else
            {
                Q13PlacementStatusText.Text = $"Click point {_q13ManualPoints.Count + 1} of 3: top-left, top-right, bottom-right.";
            }

            DrawQ13PlacementOverlay();
            return;
        }

        (_q13DragKind, _q13DragSampleIndex) = HitTestQ13Placement(imagePoint);
        _q13LastDragImagePoint = imagePoint;
        if (_q13DragKind != Q13PlacementDragKind.None)
        {
            Q13PlacementHost.CaptureMouse();
        }
    }

    private void Q13PlacementHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (_q13DragKind == Q13PlacementDragKind.None || _q13LastDragImagePoint is null)
        {
            return;
        }

        var imagePoint = Q13PlacementImagePoint(e.GetPosition(Q13PlacementHost));
        if (imagePoint is null || _pendingQ13Geometry is null)
        {
            return;
        }

        var previous = _q13LastDragImagePoint;
        var deltaX = imagePoint.X - previous.X;
        var deltaY = imagePoint.Y - previous.Y;

        switch (_q13DragKind)
        {
            case Q13PlacementDragKind.Strip:
                _pendingQ13Geometry = _pendingQ13Geometry.Translate(deltaX, deltaY);
                break;
            case Q13PlacementDragKind.TopLeft:
                _pendingQ13Geometry = _pendingQ13Geometry.ResizeFromCorner(Q13StripCorner.TopLeft, imagePoint);
                break;
            case Q13PlacementDragKind.TopRight:
                _pendingQ13Geometry = _pendingQ13Geometry.ResizeFromCorner(Q13StripCorner.TopRight, imagePoint);
                break;
            case Q13PlacementDragKind.BottomRight:
                _pendingQ13Geometry = _pendingQ13Geometry.ResizeFromCorner(Q13StripCorner.BottomRight, imagePoint);
                break;
            case Q13PlacementDragKind.Sample:
                MoveQ13Sample(imagePoint);
                break;
            case Q13PlacementDragKind.Rotation:
                RotateQ13Placement(previous, imagePoint);
                break;
        }

        _q13LastDragImagePoint = imagePoint;
        DrawQ13PlacementOverlay();
    }

    private void Q13PlacementHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _q13DragKind = Q13PlacementDragKind.None;
        _q13DragSampleIndex = -1;
        _q13LastDragImagePoint = null;
        Q13PlacementHost.ReleaseMouseCapture();
    }

    private async void Q13ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQ13Result is null)
        {
            return;
        }

        var dialog = CreateCsvReportSaveDialog("Export Q13 CSV", _currentQ13Result.ImagePath);

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _currentQ13Result.SaveCsvAsync(dialog.FileName);
            StatusText.Text = $"Q13 CSV exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 CSV export failed.";
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowQ13Result(Q13MeasurementResult result)
    {
        Q13ResultsGrid.ItemsSource = result.Patches;
        Q13SamplingText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.SamplingPixelsPerInch:0.0} pix/inch ({result.SamplingPixelsPerInch / 25.4:0.0} pix/mm)");
        Q13SampleSizeText.Text = result.SampleDataSize.ToString(CultureInfo.InvariantCulture);
        Q13GammaText.Text = result.InverseGamma.ToString("0.00", CultureInfo.InvariantCulture);
        Q13PatchCountText.Text = result.Patches.Count.ToString(CultureInfo.InvariantCulture);
        Q13ExportButton.IsEnabled = true;
        Q13ClearPointsButton.IsEnabled = true;
        DrawQ13Overlay();
        StatusText.Text = _q13SampleCenters is null
            ? "Q13 measurement complete using selected sample regions."
            : "Q13 measurement complete using imported results CSV sample centers.";
    }

    private void OpenQ13Image(string imagePath, bool autoLoadNeighborCsv)
    {
        Q13FileNameText.Text = imagePath;
        _acceptedQ13Geometry = null;
        _acceptedQ13Regions = null;
        _q13SampleCenters = null;
        Q13ClearPointsButton.IsEnabled = false;

        if (autoLoadNeighborCsv)
        {
            var resultCsvPath = IOPath.ChangeExtension(imagePath, ".csv");
            if (File.Exists(resultCsvPath))
            {
                try
                {
                    MeasureQ13FromResultsCsv(imagePath, resultCsvPath, $"Loaded neighboring Q13 results CSV: {resultCsvPath}");
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Neighboring Q13 CSV could not be loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        StatusText.Text = "Detecting Q13 grayscale strip...";
        BeginQ13Placement(imagePath);
    }

    private void MeasureQ13FromResultsCsv(string imagePath, string csvPath, string statusText)
    {
        var importedSamples = Q13ResultSampleCsv.Load(csvPath);
        _q13SampleCenters = importedSamples.Centers;
        _acceptedQ13Geometry = null;
        _acceptedQ13Regions = null;
        _pendingQ13ImagePath = null;
        _pendingQ13Geometry = null;
        _pendingQ13Regions = [];
        _q13ManualPoints.Clear();

        _currentQ13Result = _q13Measurer.Measure(imagePath, new Q13MeasurementOptions
        {
            SampleCenters = _q13SampleCenters,
            SampleSize = importedSamples.SampleSize
        });
        Q13FileNameText.Text = imagePath;
        Q13PreviewImage.Source = LoadBitmap(imagePath);
        Q13ResultsView.Visibility = Visibility.Visible;
        Q13PlacementEditor.Visibility = Visibility.Collapsed;
        ShowQ13Result(_currentQ13Result);
        StatusText.Text = statusText;
    }

    private void BeginQ13Placement(string imagePath)
    {
        _pendingQ13ImagePath = imagePath;
        _currentQ13Result = null;
        _q13ManualPoints.Clear();
        Q13ResultsView.Visibility = Visibility.Collapsed;
        Q13PlacementEditor.Visibility = Visibility.Visible;
        Q13PlacementImage.Source = LoadBitmap(imagePath);
        Q13AcceptPlacementButton.IsEnabled = false;
        Q13ExportButton.IsEnabled = false;
        Q13ClearPointsButton.IsEnabled = false;

        var detection = _q13Detector.Detect(imagePath);
        if (detection.Found && detection.Geometry is not null)
        {
            _pendingQ13Geometry = detection.Geometry;
            _pendingQ13Regions = Q13StripGeometry.CreateDefaultSampleRegions().ToList();
            Q13AcceptPlacementButton.IsEnabled = true;
            Q13PlacementStatusText.Text = "Q13 strip detected. Drag the strip, handles, rotation control, or red squares; press Enter or Accept to measure.";
        }
        else
        {
            _pendingQ13Geometry = null;
            _pendingQ13Regions = [];
            Q13PlacementStatusText.Text = "Q13 strip was not detected. Click top-left, top-right, then bottom-right to define it manually.";
        }

        DrawQ13PlacementOverlay();
        Q13PlacementHost.Focus();
    }

    private void Q13AcceptPlacementButton_Click(object sender, RoutedEventArgs e)
    {
        AcceptQ13Placement();
    }

    private void Q13CancelPlacementButton_Click(object sender, RoutedEventArgs e)
    {
        CancelQ13Placement();
    }

    private void AcceptQ13Placement()
    {
        if (_pendingQ13ImagePath is null || _pendingQ13Geometry is null)
        {
            return;
        }

        try
        {
            StatusText.Text = "Measuring accepted Q13 placement...";
            _currentQ13Result = _q13Measurer.Measure(_pendingQ13ImagePath, new Q13MeasurementOptions
            {
                StripGeometry = _pendingQ13Geometry,
                SampleRegions = _pendingQ13Regions
            });
            _acceptedQ13Geometry = _pendingQ13Geometry;
            _acceptedQ13Regions = _pendingQ13Regions.ToList();
            Q13PreviewImage.Source = LoadBitmap(_pendingQ13ImagePath);
            Q13ResultsView.Visibility = Visibility.Visible;
            Q13PlacementEditor.Visibility = Visibility.Collapsed;
            ShowQ13Result(_currentQ13Result);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelQ13Placement()
    {
        Q13PlacementEditor.Visibility = Visibility.Collapsed;
        Q13ResultsView.Visibility = Visibility.Visible;
        Q13PlacementCanvas.Children.Clear();
        _pendingQ13ImagePath = null;
        _pendingQ13Geometry = null;
        _pendingQ13Regions = [];
        _q13ManualPoints.Clear();
        StatusText.Text = "Q13 placement cancelled.";
    }

    private void DrawQ13Overlay()
    {
        Q13OverlayCanvas.Children.Clear();
        if (_currentQ13Result is null || Q13PreviewImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var transform = ImageDisplayTransform(
            Q13PreviewHost.ActualWidth,
            Q13PreviewHost.ActualHeight,
            bitmap.PixelWidth,
            bitmap.PixelHeight);

        if (transform is null)
        {
            return;
        }

        if (_acceptedQ13Geometry is not null && _acceptedQ13Regions is not null)
        {
            foreach (var region in _acceptedQ13Regions)
            {
                var polygon = new Polygon
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    Points = new PointCollection(Q13SampleRegionCorners(_acceptedQ13Geometry, region).Select(point => ImageToQ13PreviewDisplayPoint(point, bitmap, transform.Value)))
                };
                Q13OverlayCanvas.Children.Add(polygon);
            }

            return;
        }

        foreach (var patch in _currentQ13Result.Patches)
        {
            var topLeft = ImageToQ13PreviewDisplayPoint(
                new Q13Point(
                    patch.SampleReportCenterX - patch.SampleReportWidth / 2.0,
                    patch.SampleReportCenterY - patch.SampleReportHeight / 2.0),
                bitmap,
                transform.Value);
            var rectangle = new Rectangle
            {
                Width = patch.SampleReportWidth / (double)bitmap.PixelWidth * transform.Value.DisplayedWidth,
                Height = patch.SampleReportHeight / (double)bitmap.PixelHeight * transform.Value.DisplayedHeight,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(rectangle, topLeft.X);
            Canvas.SetTop(rectangle, topLeft.Y);
            Q13OverlayCanvas.Children.Add(rectangle);
        }
    }

    private void DrawQ13PlacementOverlay()
    {
        Q13PlacementCanvas.Children.Clear();
        if (Q13PlacementImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        if (_pendingQ13Geometry is null)
        {
            foreach (var point in _q13ManualPoints)
            {
                AddPlacementHandle(Q13PlacementCanvas, point, Brushes.DeepSkyBlue);
            }

            return;
        }

        var points = new[]
        {
            _pendingQ13Geometry.TopLeft,
            _pendingQ13Geometry.TopRight,
            _pendingQ13Geometry.BottomRight,
            _pendingQ13Geometry.BottomLeft
        };

        var polygon = new Polygon
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 191, 255)),
            Points = new PointCollection(points.Select(ImageToQ13PlacementDisplayPoint))
        };
        Q13PlacementCanvas.Children.Add(polygon);

        foreach (var region in _pendingQ13Regions)
        {
            var samplePolygon = new Polygon
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Points = new PointCollection(Q13SampleRegionCorners(region).Select(ImageToQ13PlacementDisplayPoint))
            };
            Q13PlacementCanvas.Children.Add(samplePolygon);
        }

        AddPlacementHandle(Q13PlacementCanvas, _pendingQ13Geometry.TopLeft, Brushes.DeepSkyBlue);
        AddPlacementHandle(Q13PlacementCanvas, _pendingQ13Geometry.TopRight, Brushes.DeepSkyBlue);
        AddPlacementHandle(Q13PlacementCanvas, _pendingQ13Geometry.BottomRight, Brushes.DeepSkyBlue);

        var rotationHandle = Q13RotationHandlePoint();
        var topCenter = _pendingQ13Geometry.PointAt(0.5, 0);
        var line = new Line
        {
            X1 = ImageToQ13PlacementDisplayPoint(topCenter).X,
            Y1 = ImageToQ13PlacementDisplayPoint(topCenter).Y,
            X2 = ImageToQ13PlacementDisplayPoint(rotationHandle).X,
            Y2 = ImageToQ13PlacementDisplayPoint(rotationHandle).Y,
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1
        };
        Q13PlacementCanvas.Children.Add(line);
        AddPlacementHandle(Q13PlacementCanvas, rotationHandle, Brushes.Orange);
    }

    private (Q13PlacementDragKind Kind, int SampleIndex) HitTestQ13Placement(Q13Point imagePoint)
    {
        if (_pendingQ13Geometry is null)
        {
            return (Q13PlacementDragKind.None, -1);
        }

        var tolerance = Math.Max(10, _pendingQ13Geometry.Height * 0.25);
        if (Distance(imagePoint, _pendingQ13Geometry.TopLeft) <= tolerance) return (Q13PlacementDragKind.TopLeft, -1);
        if (Distance(imagePoint, _pendingQ13Geometry.TopRight) <= tolerance) return (Q13PlacementDragKind.TopRight, -1);
        if (Distance(imagePoint, _pendingQ13Geometry.BottomRight) <= tolerance) return (Q13PlacementDragKind.BottomRight, -1);
        if (Distance(imagePoint, Q13RotationHandlePoint()) <= tolerance) return (Q13PlacementDragKind.Rotation, -1);

        for (var i = 0; i < _pendingQ13Regions.Count; i++)
        {
            var region = _pendingQ13Regions[i];
            var center = _pendingQ13Geometry.PointAt(region.CenterX, region.CenterY);
            if (Distance(imagePoint, center) <= Math.Max(tolerance, _pendingQ13Geometry.Height * region.Size))
            {
                return (Q13PlacementDragKind.Sample, i);
            }
        }

        var normalized = ToQ13NormalizedPoint(imagePoint);
        if (normalized is { X: >= 0 and <= 1, Y: >= 0 and <= 1 })
        {
            return (Q13PlacementDragKind.Strip, -1);
        }

        return (Q13PlacementDragKind.None, -1);
    }

    private void MoveQ13Sample(Q13Point imagePoint)
    {
        if (_q13DragSampleIndex < 0)
        {
            return;
        }

        var normalized = ToQ13NormalizedPoint(imagePoint);
        if (normalized is null)
        {
            return;
        }

        var region = _pendingQ13Regions[_q13DragSampleIndex];
        _pendingQ13Regions[_q13DragSampleIndex] = region.MoveTo(
            Math.Clamp(normalized.Value.X, 0, 1),
            Math.Clamp(normalized.Value.Y, 0, 1));
    }

    private void RotateQ13Placement(Q13Point previous, Q13Point current)
    {
        if (_pendingQ13Geometry is null)
        {
            return;
        }

        var center = _pendingQ13Geometry.Center;
        var previousAngle = Math.Atan2(previous.Y - center.Y, previous.X - center.X);
        var currentAngle = Math.Atan2(current.Y - center.Y, current.X - center.X);
        _pendingQ13Geometry = _pendingQ13Geometry.Rotate(currentAngle - previousAngle);
    }

    private Q13Point? Q13PlacementImagePoint(Point displayPoint)
    {
        if (Q13PlacementImage.Source is not BitmapSource bitmap)
        {
            return null;
        }

        var transform = ImageDisplayTransform(
            Q13PlacementHost.ActualWidth,
            Q13PlacementHost.ActualHeight,
            bitmap.PixelWidth,
            bitmap.PixelHeight);
        if (transform is null)
        {
            return null;
        }

        var (displayedWidth, displayedHeight, offsetX, offsetY) = transform.Value;
        var x = (displayPoint.X - offsetX) / displayedWidth * bitmap.PixelWidth;
        var y = (displayPoint.Y - offsetY) / displayedHeight * bitmap.PixelHeight;
        if (x < 0 || y < 0 || x > bitmap.PixelWidth || y > bitmap.PixelHeight)
        {
            return null;
        }

        return new Q13Point(x, y);
    }

    private Point ImageToQ13PlacementDisplayPoint(Q13Point imagePoint)
    {
        if (Q13PlacementImage.Source is not BitmapSource bitmap)
        {
            return new Point();
        }

        var transform = ImageDisplayTransform(
            Q13PlacementHost.ActualWidth,
            Q13PlacementHost.ActualHeight,
            bitmap.PixelWidth,
            bitmap.PixelHeight);
        if (transform is null)
        {
            return new Point();
        }

        var (displayedWidth, displayedHeight, offsetX, offsetY) = transform.Value;
        return new Point(
            offsetX + imagePoint.X / bitmap.PixelWidth * displayedWidth,
            offsetY + imagePoint.Y / bitmap.PixelHeight * displayedHeight);
    }

    private (double X, double Y)? ToQ13NormalizedPoint(Q13Point imagePoint)
    {
        if (_pendingQ13Geometry is null)
        {
            return null;
        }

        var origin = _pendingQ13Geometry.TopLeft;
        var xAxis = new Q13Point(_pendingQ13Geometry.TopRight.X - origin.X, _pendingQ13Geometry.TopRight.Y - origin.Y);
        var yAxis = new Q13Point(_pendingQ13Geometry.BottomLeft.X - origin.X, _pendingQ13Geometry.BottomLeft.Y - origin.Y);
        var point = new Q13Point(imagePoint.X - origin.X, imagePoint.Y - origin.Y);
        var determinant = xAxis.X * yAxis.Y - xAxis.Y * yAxis.X;
        if (Math.Abs(determinant) < 0.0001)
        {
            return null;
        }

        var x = (point.X * yAxis.Y - point.Y * yAxis.X) / determinant;
        var y = (xAxis.X * point.Y - xAxis.Y * point.X) / determinant;
        return (x, y);
    }

    private IReadOnlyList<Q13Point> Q13SampleRegionCorners(Q13SampleRegion region)
    {
        if (_pendingQ13Geometry is null)
        {
            return [];
        }

        return Q13SampleRegionCorners(_pendingQ13Geometry, region);
    }

    private static IReadOnlyList<Q13Point> Q13SampleRegionCorners(Q13StripGeometry geometry, Q13SampleRegion region)
    {
        var halfHeight = region.Size / 2.0;
        var halfWidth = halfHeight * geometry.Height / Math.Max(1, geometry.Width);
        return
        [
            geometry.PointAt(region.CenterX - halfWidth, region.CenterY - halfHeight),
            geometry.PointAt(region.CenterX + halfWidth, region.CenterY - halfHeight),
            geometry.PointAt(region.CenterX + halfWidth, region.CenterY + halfHeight),
            geometry.PointAt(region.CenterX - halfWidth, region.CenterY + halfHeight)
        ];
    }

    private static Point ImageToQ13PreviewDisplayPoint(
        Q13Point imagePoint,
        BitmapSource bitmap,
        (double DisplayedWidth, double DisplayedHeight, double OffsetX, double OffsetY) transform)
    {
        return new Point(
            transform.OffsetX + imagePoint.X / bitmap.PixelWidth * transform.DisplayedWidth,
            transform.OffsetY + imagePoint.Y / bitmap.PixelHeight * transform.DisplayedHeight);
    }

    private Q13Point Q13RotationHandlePoint()
    {
        var topCenter = _pendingQ13Geometry!.PointAt(0.5, 0);
        var center = _pendingQ13Geometry.Center;
        var dx = topCenter.X - center.X;
        var dy = topCenter.Y - center.Y;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var offset = Math.Max(30, _pendingQ13Geometry.Height * 0.75);
        return new Q13Point(topCenter.X + dx / length * offset, topCenter.Y + dy / length * offset);
    }

    private void AddPlacementHandle(Canvas canvas, Q13Point imagePoint, Brush brush)
    {
        var displayPoint = ImageToQ13PlacementDisplayPoint(imagePoint);
        var ellipse = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(ellipse, displayPoint.X - 6);
        Canvas.SetTop(ellipse, displayPoint.Y - 6);
        canvas.Children.Add(ellipse);
    }

    private static double Distance(Q13Point first, Q13Point second)
    {
        return Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
    }

    private enum Q13PlacementDragKind
    {
        None,
        Strip,
        TopLeft,
        TopRight,
        BottomRight,
        Rotation,
        Sample
    }
}
