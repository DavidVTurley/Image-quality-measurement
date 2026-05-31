using Imcheck.Measurement.Meaasurements.Uniformity;
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
            StatusText.Text = "Measuring uniformity image...";
            _currentUniformityResult = _uniformityAnalyzer.Analyze(dialog.FileName);
            UniformityPreviewImage.Source = LoadBitmap(dialog.FileName);
            UniformityFileNameText.Text = dialog.FileName;
            ShowUniformityResult(_currentUniformityResult);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UniformityExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUniformityResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export uniformity CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = IOPath.ChangeExtension(IOPath.GetFileName(_currentUniformityResult.ImagePath), ".csv")
        };

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
            var rectangle = new Rectangle
            {
                Width = sample.SampleSize / (double)_currentUniformityResult.ImageWidth * displayedWidth,
                Height = sample.SampleSize / (double)_currentUniformityResult.ImageHeight * displayedHeight,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(rectangle, offsetX + sample.SampleX / (double)_currentUniformityResult.ImageWidth * displayedWidth);
            Canvas.SetTop(rectangle, offsetY + sample.SampleY / (double)_currentUniformityResult.ImageHeight * displayedHeight);
            UniformityOverlayCanvas.Children.Add(rectangle);
        }
    }
}
