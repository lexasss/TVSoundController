using System;
using System.Windows;
using System.Windows.Controls;

namespace TVSoundController;

/// <summary>
/// The page contains controls to set TV volume adjustment settings.
/// Also, it displayed the live mic level.
/// Automatic TV volume adjustment start one the user click "Start" button
/// </summary>
public partial class MainPage : Page, IDisposable
{
    public MainPage(Controller controller)
    {
        _controller = controller;

        InitializeComponent();

        SetupController();
        DisplayMicProps();
        LoadSettings();

        Application.Current.Exit += (s, e) =>
        {
            _controller.TV.Disconnected -= TV_Disconnected;
            _controller.Dispose();

            SaveSettings();
        };

        UpdateUI();
    }

    public void Dispose()
    {
        _controller.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Controller _controller;

    double _micLevelHeight;

    private void SetupController()
    {
        _controller.IsEnabled = false;
        _controller.Mic.LevelChanged += Mic_LevelChanged;
        _controller.TV.Disconnected += TV_Disconnected;
        _controller.VolumeChanged += Controller_VolumeChanged;
    }

    private void DisplayMicProps()
    {
        foreach (var prop in _controller.Mic.Properties)
        {
            var label = new Label
            {
                Content = $"{prop.Key}: ",
                Padding = new Thickness(6, 0, 6, 0)
            };
            var value = new Label
            {
                Content = prop.Value,
                Padding = new Thickness(6, 0, 6, 0)
            };
            var container = new WrapPanel();
            container.Children.Add(label);
            container.Children.Add(value);
            stpMicProps.Children.Add(container);
        }
    }

    private void LoadSettings()
    {
        sldMicMinLevel.Value = App.Settings.MicMinLevel;
        sldMicMaxLevel.Value = App.Settings.MicMaxLevel;
        sldMicScale.Value = App.Settings.MicScale;
        sldTVVolumeInitial.Value = App.Settings.TVVolumeInitial;
        sldTVVolumeMax.Value = App.Settings.TVVolumeMax;
    }

    private void SaveSettings()
    {
        App.Settings.MicMinLevel = sldMicMinLevel.Value;
        App.Settings.MicMaxLevel = sldMicMaxLevel.Value;
        App.Settings.MicScale = sldMicScale.Value;
        App.Settings.TVVolumeInitial = (int)sldTVVolumeInitial.Value;
        App.Settings.TVVolumeMax = (int)sldTVVolumeMax.Value;
        App.Settings.Save();
    }

    private void UpdateUI()
    {
        if (!IsLoaded)
            return;

        btnStartStop.IsEnabled = _controller.TV.IsConnected;
        btnStartStop.Content = _controller.IsEnabled ? "Stop" : "Start";
        
        lblIsTVConnected.Content = _controller.TV.IsConnected ? "connected" : "disconnected";

        var offsetMin = (double)sldMicMinLevel.Value / 100 * _micLevelHeight;
        rctMicMinLevel.Margin = new Thickness(0, 0, 0, offsetMin);

        var offsetMinInt = ((double)sldMicMinLevel.Value + _controller.RangeThresholdDetector.MinHist) / 100 * _micLevelHeight;
        rctMicMinIntLevel.Margin = new Thickness(0, 0, 0, offsetMinInt);

        var offsetMax = (double)sldMicMaxLevel.Value / 100 * _micLevelHeight;
        rctMicMaxLevel.Margin = new Thickness(0, 0, 0, offsetMax);

        var offsetMaxInt = ((double)sldMicMaxLevel.Value - _controller.RangeThresholdDetector.MaxHist) / 100 * _micLevelHeight;
        rctMicMaxIntLevel.Margin = new Thickness(0, 0, 0, offsetMaxInt);
    }

    private void Mic_LevelChanged(object? sender, double e)
    {
        Dispatcher.Invoke(() =>
        {
            var levelNormalized = e / 0xFFFF;
            rctMicLevel.Height = levelNormalized * _micLevelHeight;
        });
    }

    private void TV_Disconnected(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_controller.IsEnabled)
            {
                _controller.IsEnabled = false;
            }

            UpdateUI();
        });
    }

    private void Controller_VolumeChanged(object? sender, int e)
    {
        Dispatcher.Invoke(() =>
        {
            sldTVVolumeInitial.Value = e;
        });
    }

    // UI

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _micLevelHeight = bdrMeter.ActualHeight;

        MicMinLevel_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, sldMicMinLevel.Value));
        MicMaxLevel_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, sldMicMaxLevel.Value));
        MicScale_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, sldMicScale.Value));
        TVVolumeMax_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, sldTVVolumeMax.Value));
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _micLevelHeight = bdrMeter.ActualHeight;
        UpdateUI();
    }

    private void MicMinLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            _controller.RangeThresholdDetector.Min = sldMicMinLevel.Value;
            UpdateUI();
        }
        catch (ArgumentException)
        {
            sldMicMinLevel.Value = _controller.RangeThresholdDetector.Min;
        }
    }

    private void MicMaxLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try
        {
            _controller.RangeThresholdDetector.Max = sldMicMaxLevel.Value;
            UpdateUI();
        }
        catch (ArgumentException)
        {
            sldMicMaxLevel.Value = _controller.RangeThresholdDetector.Max;
        }
    }

    private void MicScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _controller.Mic.Scale = sldMicScale.Value;
    }

    private void TVVolumeMax_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _controller.TVVolumeMax = (int)sldTVVolumeMax.Value;
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        _controller.IsEnabled = !_controller.IsEnabled;
        if (_controller.IsEnabled)
        {
            _controller.TVVolume = (int)sldTVVolumeInitial.Value;
        }

        UpdateUI();
    }
}
