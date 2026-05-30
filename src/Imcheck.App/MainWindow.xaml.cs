using Imcheck.Measurement;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Imcheck.App;

public partial class MainWindow : Window
{
    private readonly Q13Measurer _measurer = new();
    private Q13MeasurementResult? _currentResult;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
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
            StatusText.Text = "Measuring image...";
            _currentResult = _measurer.Measure(dialog.FileName);
            PreviewImage.Source = LoadBitmap(dialog.FileName);
            ResultsGrid.ItemsSource = _currentResult.Patches;
            FileNameText.Text = dialog.FileName;
            SamplingText.Text = string.Create(CultureInfo.InvariantCulture, $"{_currentResult.SamplingPixelsPerInch:0.0} pix/inch ({_currentResult.SamplingPixelsPerInch / 25.4:0.0} pix/mm)");
            SampleSizeText.Text = _currentResult.SampleDataSize.ToString(CultureInfo.InvariantCulture);
            GammaText.Text = _currentResult.InverseGamma.ToString("0.00", CultureInfo.InvariantCulture);
            PatchCountText.Text = _currentResult.Patches.Count.ToString(CultureInfo.InvariantCulture);
            ExportButton.IsEnabled = true;
            StatusText.Text = "Measurement complete.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = Path.ChangeExtension(_currentResult.ImageName, ".csv")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _currentResult.SaveCsvAsync(dialog.FileName);
            StatusText.Text = $"CSV exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "CSV export failed.";
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
