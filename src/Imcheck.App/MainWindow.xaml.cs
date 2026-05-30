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
    private IReadOnlyList<Q13SamplePoint>? _sampleCenters;

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
            _currentResult = _measurer.Measure(dialog.FileName, new Q13MeasurementOptions { SampleCenters = _sampleCenters });
            PreviewImage.Source = LoadBitmap(dialog.FileName);
            FileNameText.Text = dialog.FileName;
            ShowResult(_currentResult);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadPointsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open sample point CSV",
            Filter = "CSV or text files|*.csv;*.txt|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _sampleCenters = Q13SamplePointCsv.Load(dialog.FileName);
            ClearPointsButton.IsEnabled = true;
            StatusText.Text = $"Loaded {_sampleCenters.Count} explicit sample centers.";

            if (_currentResult is not null)
            {
                _currentResult = _measurer.Measure(_currentResult.ImagePath, new Q13MeasurementOptions { SampleCenters = _sampleCenters });
                ShowResult(_currentResult);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load sample points", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearPointsButton_Click(object sender, RoutedEventArgs e)
    {
        _sampleCenters = null;
        ClearPointsButton.IsEnabled = false;
        StatusText.Text = "Using automatic straight-line sample centers.";

        if (_currentResult is not null)
        {
            _currentResult = _measurer.Measure(_currentResult.ImagePath);
            ShowResult(_currentResult);
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

    private void ShowResult(Q13MeasurementResult result)
    {
        ResultsGrid.ItemsSource = result.Patches;
        SamplingText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.SamplingPixelsPerInch:0.0} pix/inch ({result.SamplingPixelsPerInch / 25.4:0.0} pix/mm)");
        SampleSizeText.Text = result.SampleDataSize.ToString(CultureInfo.InvariantCulture);
        GammaText.Text = result.InverseGamma.ToString("0.00", CultureInfo.InvariantCulture);
        PatchCountText.Text = result.Patches.Count.ToString(CultureInfo.InvariantCulture);
        ExportButton.IsEnabled = true;
        StatusText.Text = _sampleCenters is null
            ? "Measurement complete using automatic straight-line centers."
            : "Measurement complete using explicit sample centers.";
    }

}
