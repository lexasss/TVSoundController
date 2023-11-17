using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TVSoundController;

/// <summary>
/// Receive sound from a microphone
/// </summary>
public class Microphone : IDisposable
{
    public event EventHandler<double>? LevelChanged;

    /// <summary>
    /// Scale applied to to the sound input (multiplier).
    /// This is need as input signal may my vary across mics due to various condition.
    /// </summary>
    public double Scale { get; set; } = 1;

    /// <summary>
    /// List of connected microphons
    /// </summary>
    public static string[] Devices
    {
        get
        {
            var result = new List<string>();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var devCaps = WaveInEvent.GetCapabilities(i);
                result.Add(devCaps.ProductName);
            }
            return result.ToArray();
        }
    }

    /// <summary>
    /// Properties of the active microphone
    /// </summary>
    public ImmutableDictionary<string, object> Properties
    {
        get
        {
            var result = new Dictionary<string, object>();
            var mixerLine = _waveIn.GetMixerLine();
            foreach (var ctrl in mixerLine.Controls)
            {
                object value = ctrl switch
                {
                    NAudio.Mixer.UnsignedMixerControl umc => umc.Percent,
                    NAudio.Mixer.SignedMixerControl smc => smc.Percent,
                    NAudio.Mixer.BooleanMixerControl bmc => bmc.Value,
                    _ => ctrl.ToString()
                };
                result.Add(ctrl.Name, value);
            }
            return result.ToImmutableDictionary();
        }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="deviceId">Microphone ID as it appears in the <see cref="Devices"/> list</param>
    public Microphone(int deviceId)
    {
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceId
        };

        _waveIn.DataAvailable += WaveIn_DataAvailable;
    }

    /// <summary>
    /// Starts sound capturing
    /// </summary>
    public void Start()
    {
        _waveIn.StartRecording();
    }

    /// <summary>
    /// Stop sound capturing
    /// </summary>
    public void Stop()
    {
        _waveIn.StopRecording();
    }

    public void Dispose()
    {
        _waveIn.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    const int BufferLength = 5;

    readonly WaveInEvent _waveIn;
    readonly Queue<double> _buffer = new();

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs a)
    {
        if (a.BytesRecorded == 0)
            return;

        var bytesPerSample = _waveIn.WaveFormat.BitsPerSample / 8;

        double sum = 0;
        for (int i = 0; (i + bytesPerSample) <= a.Buffer.Length; i += bytesPerSample)
        {
            int value = 0;

            for (int j = bytesPerSample - 1; j >= 0; j--)
                value = (value << 8) + a.Buffer[i + j];

            if (bytesPerSample == 1)
                sum += Math.Abs(value > 0x7F ? -255 + value : value);
            else if (bytesPerSample == 2)
                sum += Math.Abs(value > 0x7FFF ? -65535 + value : value);
            else
                sum += Math.Abs(value);
        }
        double avg = sum / (a.Buffer.Length / bytesPerSample) * Scale;

        _buffer.Enqueue(avg);

        if (_buffer.Count > BufferLength)
        {
            _buffer.Dequeue();
            var level = _buffer.Aggregate((value, acc) => value + acc) / BufferLength;
            LevelChanged?.Invoke(this, level);
        }
    }
}
