using System;
using System.Diagnostics;
using System.Timers;

namespace TVSoundController;

/// <summary>
/// Implements the logic that takes sound volume (level) estimated is <see cref="Microphone"/> as and input
/// and send volume-up and volume-down commands the connected TV to maintain the TV volume level not too low
/// and not too high.
/// </summary>
public class Controller: IDisposable
{
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!_isEnabled)
            {
                _keySendingTimer.Stop();
            }
        }
    }

    /// <summary>
    /// Actual TV volume as it is shown in the connected TV.
    /// Note that this value must be set be set for using the controller
    /// </summary>
    public int TVVolume { get; set; }

    /// <summary>
    /// Maximum TV volume that is allowed to be set
    /// </summary>
    public int TVVolumeMax { get; set; }

    public Microphone Mic => _mic;
    public Samsung.TV TV => _samsungTV;
    public RangeThresholdDetector RangeThresholdDetector => _rtDetector;

    /// <summary>
    /// Fires when a request to change the volume was just sent to the connected TV.
    /// </summary>
    public event EventHandler<int>? VolumeChanged;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="deviceId">Microphone ID as it appears in the <see cref="Microphone.Devices"/> list</param>
    /// <param name="samsungTV">TV WebSocket client</param>
    public Controller(int deviceId, Samsung.TV samsungTV)
    {
        _samsungTV = samsungTV;

        _rtDetector = new(App.Settings.MicMinLevel, App.Settings.MicMaxLevel, MicMinHist, MicMaxHist);

        _mic = new Microphone(deviceId);
        _mic.LevelChanged += Mic_LevelChanged;

        _keySendingTimer.Elapsed += Timer_Elapsed;
        _keySendingTimer.AutoReset = true;

        _mic.Start();
    }

    public void Dispose()
    {
        _samsungTV.Dispose();
        _mic.Dispose();
        _keySendingTimer.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    const double MicMinHist = 3;
    const double MicMaxHist = 5;

    readonly Samsung.TV _samsungTV;
    readonly Microphone _mic;
    readonly RangeThresholdDetector _rtDetector;

    readonly Timer _keySendingTimer = new();

    string _tvKey = string.Empty;
    bool _isEnabled = true;

    private void Mic_LevelChanged(object? sender, double e)
    {
        var levelNormalized = e / 0xFFFF;

        var cross = _rtDetector.Feed(levelNormalized * 100);
        if (cross == RangeThresholdDetector.Cross.ExitUp)
        {
            StartVolumeDecrease();
        }
        else if (cross == RangeThresholdDetector.Cross.ExitDown)
        {
            StartVolumeIncrease();
        }
        else if (cross == RangeThresholdDetector.Cross.Enter)
        {
            _keySendingTimer.Stop();
        }
    }

    private void StartVolumeDecrease()
    {
        _keySendingTimer.Stop();

        _tvKey = Samsung.Keys.VOLDOWN;

        _keySendingTimer.Interval = 300;
        _keySendingTimer.Start();
    }

    private void StartVolumeIncrease()
    {
        _keySendingTimer.Stop();

        _tvKey = Samsung.Keys.VOLUP;

        _keySendingTimer.Interval = 2000;
        _keySendingTimer.Start();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isEnabled && !string.IsNullOrEmpty(_tvKey) && _samsungTV.IsConnected)
        {
            Debug.WriteLine($"Volume {_tvKey}");
            var volume = TVVolume + (_tvKey == Samsung.Keys.VOLUP ? 1 : -1);

            if (volume > TVVolumeMax)
            {
                _keySendingTimer.Stop();
                return;
            }

            TVVolume = volume;
            _samsungTV.Press(_tvKey);

            VolumeChanged?.Invoke(this, TVVolume);
        }
    }
}
