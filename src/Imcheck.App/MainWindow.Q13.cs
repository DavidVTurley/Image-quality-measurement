using Imcheck.Measurement.Meaasurements.Q13;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using IOPath = System.IO.Path;

namespace Imcheck.App;

public partial class MainWindow
{
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
            StatusText.Text = "Measuring Q13 image...";
            _currentQ13Result = _q13Measurer.Measure(dialog.FileName, new Q13MeasurementOptions { SampleCenters = _q13SampleCenters });
            Q13PreviewImage.Source = LoadBitmap(dialog.FileName);
            Q13FileNameText.Text = dialog.FileName;
            ShowQ13Result(_currentQ13Result);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13LoadPointsButton_Click(object sender, RoutedEventArgs e)
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
            _q13SampleCenters = Q13SamplePointCsv.Load(dialog.FileName);
            Q13ClearPointsButton.IsEnabled = true;
            StatusText.Text = $"Loaded {_q13SampleCenters.Count} explicit Q13 sample centers.";

            if (_currentQ13Result is not null)
            {
                _currentQ13Result = _q13Measurer.Measure(_currentQ13Result.ImagePath, new Q13MeasurementOptions { SampleCenters = _q13SampleCenters });
                ShowQ13Result(_currentQ13Result);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load sample points", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13ClearPointsButton_Click(object sender, RoutedEventArgs e)
    {
        _q13SampleCenters = null;
        Q13ClearPointsButton.IsEnabled = false;
        StatusText.Text = "Using automatic Q13 straight-line sample centers.";

        if (_currentQ13Result is not null)
        {
            _currentQ13Result = _q13Measurer.Measure(_currentQ13Result.ImagePath);
            ShowQ13Result(_currentQ13Result);
        }
    }

    private async void Q13ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQ13Result is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Q13 CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = IOPath.ChangeExtension(_currentQ13Result.ImageName, ".csv")
        };

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
        StatusText.Text = _q13SampleCenters is null
            ? "Q13 measurement complete using automatic straight-line centers."
            : "Q13 measurement complete using explicit sample centers.";
    }
}
