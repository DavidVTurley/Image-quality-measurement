using Imcheck.Measurement.Measurements.Uniformity;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IOPath = System.IO.Path;

namespace Imcheck.App;

public partial class MainWindow
{
    private void UniformityOpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open uniformity image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            OpenUniformityImage(dialog.FileName, autoLoadNeighborCsv: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UniformityOpenImageCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var imageDialog = new OpenFileDialog
        {
            Title = "Open uniformity image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (imageDialog.ShowDialog(this) != true)
        {
            return;
        }

        var csvDialog = new OpenFileDialog
        {
            Title = "Open uniformity results CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            InitialDirectory = IOPath.GetDirectoryName(imageDialog.FileName)
        };

        if (csvDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            AnalyzeUniformityFromResultsCsv(imageDialog.FileName, csvDialog.FileName, "Imported uniformity image and results CSV.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity results CSV import failed.";
            MessageBox.Show(this, ex.Message, "Could not load uniformity results CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UniformityExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUniformityResult is null)
        {
            return;
        }

        var dialog = CreateCsvReportSaveDialog("Export uniformity CSV", _currentUniformityResult.ImagePath);

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, _currentUniformityResult.ToCsv());
            StatusText.Text = $"Uniformity CSV exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity CSV export failed.";
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UniformityPreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawUniformityOverlay();
    }

    private void ShowUniformityResult(UniformityAnalysisResult result)
    {
        UniformityResultsGrid.ItemsSource = result.Samples;
        UniformityImageText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.ImageWidth}x{result.ImageHeight}, {result.BitDepth}-bit");
        UniformitySampleSizeText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.SampleSize}x{result.SampleSize}");
        UniformityDeltaLText.Text = result.MaxDeltaLStar.ToString("0.####", CultureInfo.InvariantCulture);
        UniformityDeltaEText.Text = result.MaxDeltaEab.ToString("0.####", CultureInfo.InvariantCulture);
        UniformityExportButton.IsEnabled = true;
        DrawUniformityOverlay();
        StatusText.Text = "Uniformity measurement complete.";
    }

    private void OpenUniformityImage(string imagePath, bool autoLoadNeighborCsv)
    {
        if (autoLoadNeighborCsv)
        {
            var resultCsvPath = IOPath.ChangeExtension(imagePath, ".csv");
            if (File.Exists(resultCsvPath))
            {
                try
                {
                    AnalyzeUniformityFromResultsCsv(imagePath, resultCsvPath, $"Loaded neighboring uniformity results CSV: {resultCsvPath}");
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Neighboring uniformity CSV could not be loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        StatusText.Text = "Measuring uniformity image...";
        _currentUniformityResult = _uniformityAnalyzer.Analyze(imagePath);
        UniformityPreviewImage.Source = LoadBitmap(imagePath);
        UniformityFileNameText.Text = imagePath;
        ShowUniformityResult(_currentUniformityResult);
    }

    private void AnalyzeUniformityFromResultsCsv(string imagePath, string csvPath, string statusText)
    {
        var samples = UniformityResultSampleCsv.LoadSamples(csvPath);
        _currentUniformityResult = _uniformityAnalyzer.Analyze(imagePath, new UniformityAnalysisOptions
        {
            Samples = samples
        });
        UniformityPreviewImage.Source = LoadBitmap(imagePath);
        UniformityFileNameText.Text = imagePath;
        ShowUniformityResult(_currentUniformityResult);
        StatusText.Text = statusText;
    }

    private void DrawUniformityOverlay()
    {
        UniformityOverlayCanvas.Children.Clear();
        if (_currentUniformityResult is null || UniformityPreviewImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var transform = ImageDisplayTransform(
            UniformityPreviewHost.ActualWidth,
            UniformityPreviewHost.ActualHeight,
            bitmap.PixelWidth,
            bitmap.PixelHeight);

        if (transform is null)
        {
            return;
        }

        var (displayedWidth, displayedHeight, offsetX, offsetY) = transform.Value;
        foreach (var sample in _currentUniformityResult.Samples)
        {
            var topLeft = ImageToUniformityPreviewDisplayPoint(
                sample.SampleReportCenterX - sample.SampleReportWidth / 2.0,
                sample.SampleReportCenterY - sample.SampleReportHeight / 2.0,
                _currentUniformityResult.ImageWidth,
                _currentUniformityResult.ImageHeight,
                transform.Value);
            var rectangle = new Rectangle
            {
                Width = sample.SampleReportWidth / (double)_currentUniformityResult.ImageWidth * displayedWidth,
                Height = sample.SampleReportHeight / (double)_currentUniformityResult.ImageHeight * displayedHeight,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(rectangle, topLeft.X);
            Canvas.SetTop(rectangle, topLeft.Y);
            UniformityOverlayCanvas.Children.Add(rectangle);
        }
    }

    private static Point ImageToUniformityPreviewDisplayPoint(
        double x,
        double y,
        int imageWidth,
        int imageHeight,
        (double DisplayedWidth, double DisplayedHeight, double OffsetX, double OffsetY) transform)
    {
        return new Point(
            transform.OffsetX + x / imageWidth * transform.DisplayedWidth,
            transform.OffsetY + y / imageHeight * transform.DisplayedHeight);
    }
}
