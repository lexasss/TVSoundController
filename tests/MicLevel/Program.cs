using NAudio.Wave;

Console.WriteLine("Audio recording devices:");
for (int i = 0; i < WaveInEvent.DeviceCount; i++)
{
    var devCaps = WaveInEvent.GetCapabilities(i);
    Console.WriteLine($"{i}: {devCaps.ProductName}");
}

Console.WriteLine("\nEnter the number of the recorded device to use:");
var input = Console.ReadLine();

if (!int.TryParse(input, out int deviceId) || deviceId < 0 || deviceId >= WaveInEvent.DeviceCount)
    return;

using var waveIn = new WaveInEvent();
waveIn.DeviceNumber = deviceId;
//waveIn.WaveFormat = new WaveFormat(16000, 2);
waveIn.DataAvailable += (s, a) =>
{
    if (a.BytesRecorded == 0)
        return;

    int sum = 0;
    for (int i = 0; (i + 1)  < a.Buffer.Length; i += 2)
    {
        sum += a.Buffer[i] + (a.Buffer[i + 1] << 8);
    }
    double avg = (double)sum / (a.Buffer.Length / 2);
    Console.WriteLine(avg.ToString("F2"));
};

waveIn.StartRecording();

Console.WriteLine("\nPress ENTER to stop\n");
Console.ReadLine();

waveIn.StopRecording();
