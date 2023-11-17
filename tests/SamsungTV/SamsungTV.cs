using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace SamsungTV;

public class SamsungTV : IDisposable
{
    public SamsungTV(Settings settings)
    {
        _settings = settings;
    }

    public void Dispose()
    {
        _readingThread?.Interrupt();
        _readingTokenSource.Cancel();
        _readingThread?.Join();
        _ws?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Must be called before any other method
    /// </summary>
    /// <param name="delay">Optional delay, ms</param>
    /// <returns>True is the TV is on, false otherwise</returns>
    public async Task<bool> IsOn(int delay = 0)
    {
        if (delay > 0)
            await Task.Delay(delay);

        _isOn = false;
        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var res = await client.GetAsync($"http://{_settings.IpAddr}:8001/api/v2/");
            res.EnsureSuccessStatusCode();
            var answer = await res.Content.ReadAsStringAsync();
            Log($"TV is on [{answer}]");
            _isOn = true;
        }
        catch (Exception ex)
        {
            Log($"TV is off [{ex.Message}]");
        }

        return _isOn;
    }

    /// <summary>
    /// Turns the TV on by broadcasting a Wake-On-Line packet with the TVs MAC address.
    /// </summary>
    /// <param name="repeat">Number of packets to send</param>
    public async Task TurnOn(int repeat = 10)
    {
        var wolEndpoint = CreateWolEndpoint();

        using var client = new UdpClient() { EnableBroadcast = true };

        try
        {
            byte[] packet = CreateWolPacket(_settings.MaxAddrBytes);
            Log($"Wol packet: {Convert.ToHexString(packet)}");
            for (int i = 0; i < repeat; i++)
            {
                client.Send(packet, packet.Length, wolEndpoint);
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Log($"Cannot turn the TV on: {ex.Message}");
        }
    }

    /// <summary>
    /// Establishes a connection to the TV.
    /// If the token is not yet received, it will request it.
    /// </summary>
    /// <returns>Returns null if the TV is off or if the token is missing and cannot be received from the TV.
    /// Otherwise return the token in use.</returns>
    public async Task<string?> Connect()
    {
        if (!_isOn)
        {
            return null;
        }

        (_ws, var token) = await CreateWebSocket(new Uri(WsUrl));
        if (_ws != null)
        {
            _readingThread = new Thread(async () =>
            {
                while (_readingThread?.ThreadState == ThreadState.Running)
                {
                    var buffer = new ArraySegment<byte>(new byte[1024]);
                    await _ws.ReceiveAsync(buffer, _readingTokenSource.Token);

                    var data = Encoding.UTF8.GetString(buffer);
                    Log("Data received from TV: " + data.Trim());
                }
            });
            _readingThread.Start();

            if (token != null)
                _settings.Token = token;
        }

        if (string.IsNullOrEmpty(_settings.Token))
        {
            return null;
        }

        return _settings.Token;
    }

    /// <summary>
    /// Send a key to the TV
    /// </summary>
    /// <param name="key">A key from the list <see cref="Keys"/></param>
    /// <returns>True if the key was sent</returns>
    public async Task<bool> Press(string key) => await Send(new Command(ControlParameters.Key(key)));

    /// <summary>
    /// Send a text to a focused input window shown on th eTV
    /// </summary>
    /// <param name="text">Any text</param>
    /// <returns>True if the text was sent</returns>
    public async Task<bool> Type(string text) => await Send(new Command(ControlParameters.Text(text)));

    // Internal

    readonly Settings _settings;
    readonly CancellationTokenSource _readingTokenSource = new();

    bool _isOn = false;
    ClientWebSocket? _ws;
    Thread? _readingThread;

    private string WsProtocol => _settings.Port == 8001 ? "ws" : "wss";
    private string WsUrl => $"{WsProtocol}://{_settings.IpAddr}:{_settings.Port}/api/v2/channels/samsung.remote.control?name={_settings.AppNameBase64}" +
        (!string.IsNullOrEmpty(_settings.Token) ? $"&token={_settings.Token}" : "");


    private async Task<(ClientWebSocket?, string?)> CreateWebSocket(Uri uri)
    {
        var waitingTokenSource = new CancellationTokenSource();

        string? token = null;

        var ws = new ClientWebSocket();
        ws.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

        try
        {
            Log($"Connecting to {uri}");
            await ws.ConnectAsync(uri, CancellationToken.None);
            Log("Please accept dialog for new connection on TV, if provided");

            var buffer = new ArraySegment<byte>(new byte[1024]);
            await ws.ReceiveAsync(buffer, CancellationToken.None);

            var data = Encoding.UTF8.GetString(buffer);
            Log("Data received from TV: " + data.Trim());

            var response = Newtonsoft.Json.Linq.JObject.Parse(data);
            string? method = response?["event"]?.ToString();
            if (method?.Equals("ms.channel.connect") ?? false)
            {
                token = response?["data"]?["token"]?.ToString();

                if (token == null)  // not sure we need this...
                {
                    var clients = response?["data"]?["clients"]?.ToArray();
                    if (clients != null)
                    {
                        foreach (var client in clients)
                        {
                            var attr = client["attributes"];
                            var name = attr?["name"]?.ToString();
                            if (name == _settings.AppNameBase64)
                            {
                                token = attr?["token"]?.ToString();
                                break;
                            }
                        }
                    }
                }
                Log($"Token received: {token}");
            }

            waitingTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, ex.Message, CancellationToken.None);
            ws.Dispose();
            ws = null;

            Log($"Error on connecting to TV: {ex}");
            waitingTokenSource.Cancel();
        }

        try
        {
            await Task.Delay(30000, waitingTokenSource.Token);
            Log("Timeout on retrieving a token");
        }
        catch (OperationCanceledException) { }

        return (ws, token);
    }

    private IPEndPoint CreateWolEndpoint()
    {
        IPAddress[] host;
        try
        {
            host = Dns.GetHostAddresses(_settings.IpAddr);
        }
        catch
        {
            throw new ArgumentException($"Could not resolve address: {_settings.IpAddr}");
        }

        if (host.Length == 0)
        {
            throw new ArgumentNullException($"Could not resolve address: {_settings.IpAddr}");
        }

        IPAddress subnet = IPAddress.Parse(_settings.Subnet);
        byte[] subnetAddrBytes = subnet.GetAddressBytes();
        byte[] ipAddrBytes = host[0].GetAddressBytes();
        for (int i = 0; i < ipAddrBytes.Length; i++)
        {
            subnetAddrBytes[i] = (byte)(ipAddrBytes[i] | (subnetAddrBytes[i] ^ 0xff));
        }

        return new IPEndPoint(new IPAddress(subnetAddrBytes), _settings.Port);
    }

    private static byte[] CreateWolPacket(byte[] macAddress)
    {
        byte[] packet = new byte[0x66];
        for (int i = 0; i < 6; i++)
        {
            packet[i] = 0xff;
        }
        for (int j = 1; j <= 0x10; j++)
        {
            macAddress.CopyTo(packet, j * 6);
        }
        return packet;
    }

    private async Task<bool> Send(Command cmd)
    {
        if (_ws == null || string.IsNullOrEmpty(_settings.Token))
        {
            return false;
        }

        Log($"Sending: {cmd}");

        try
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd.ToString()));
            await _ws.SendAsync(buffer, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log($"Error on sending a command: {ex}");
            return false;
        }

        return true;
    }

    private void Log(string msg)
    {
        if (_settings.Debug)
        {
            Console.WriteLine(msg);
            Console.WriteLine("");
        }
    }
}

public class Settings
{
    public string AppName { get; init; }
    public string? Token { get; set; }
    public string IpAddr { get; init; }
    public string MacAddr { get; init; }
    public int Port { get; init; }
    public string Subnet { get; init; }
    public bool Debug { get; init; }

    public byte[] MaxAddrBytes => MacAddr.Split('-', ':').Select(b => Convert.ToByte(b, 0x10)).ToArray();

    public string AppNameBase64 => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(AppName));


    public Settings(
        string appName,
        string? token,
        string ipAddr,
        string macAddr,
        int port = 8002,
        string subnet = "255.255.255.255",
        bool debug = false)
    {
        AppName = appName;
        Token = token;
        IpAddr = ipAddr;
        MacAddr = macAddr;
        Port = port;
        Subnet = subnet;
        Debug = debug;

        if (MaxAddrBytes.Length != 6)
            throw new ArgumentException("MAC address should be 12 characters (not counting separators)");
    }

    public override string ToString() => 
        $@"AppName: {AppName}, Token: {Token}, IpAddr: {IpAddr}, MacAddr: {MacAddr}, Port: {Port}, Subnet: {Subnet}, Debug: {Debug}";
}

public class Command
{
#pragma warning disable IDE1006 // Naming Styles
    public string method { get; set; }
    public object parameters { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public Command(ControlParameters p)
    {
        method = "ms.remote.control";
        parameters = p;
    }
    public Command(AppParameters p)
    {
        method = "ms.channel.emit";
        parameters = p;
    }

    public override string ToString() => JsonConvert.SerializeObject(this)
        .Replace("parameters", "params")
        .Replace("event_", "event");
}

public class ControlParameters
{
    public string? Cmd { get; set; }
    public string? DataOfCmd { get; set; }
    public string? Option { get; set; }
    public string? TypeOfRemote { get; set; }

    public static ControlParameters Key(string key) => new()
    {
        Cmd = "Click",
        DataOfCmd = key,
        Option = "false",
        TypeOfRemote = "SendRemoteKey"
    };
    public static ControlParameters Text(string text) => new()
    {
        Cmd = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
        DataOfCmd = "base64",
        TypeOfRemote = "SendInputString"
    };
}

public class AppParameters
{
#pragma warning disable IDE1006 // Naming Styles
    public object? data { get; set; }
    public string? event_ { get; set; }
    public string? to { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public static AppParameters List() => new()
    {
        data = "",
        event_ = "ed.installedApp.get",
        to = "host",
    };
}

public static class Keys
{
    public static readonly string NUM0 = "KEY_0";
    public static readonly string NUM1 = "KEY_1";
    public static readonly string NUM2 = "KEY_2";
    public static readonly string NUM3 = "KEY_3";
    public static readonly string NUM4 = "KEY_4";
    public static readonly string NUM5 = "KEY_5";
    public static readonly string NUM6 = "KEY_6";
    public static readonly string NUM7 = "KEY_7";
    public static readonly string NUM8 = "KEY_8";
    public static readonly string NUM9 = "KEY_9";
    public static readonly string POWER = "KEY_POWER";
    public static readonly string SOURCE = "KEY_SOURCE";
    public static readonly string PLUS100 = "KEY_PLUS100"; // DASH/MINUS "-" button
    public static readonly string PRECH = "KEY_PRECH";
    public static readonly string VOLUP = "KEY_VOLUP";
    public static readonly string VOLDOWN = "KEY_VOLDOWN";
    public static readonly string MUTE = "KEY_MUTE";
    public static readonly string CH_LIST = "KEY_CH_LIST";
    public static readonly string CHDOWN = "KEY_CHDOWN";
    public static readonly string CHUP = "KEY_CHUP";
    public static readonly string HOME = "KEY_HOME";
    public static readonly string GUIDE = "KEY_GUIDE";
    public static readonly string LEFT = "KEY_LEFT";
    public static readonly string UP = "KEY_UP";
    public static readonly string RIGHT = "KEY_RIGHT";
    public static readonly string DOWN = "KEY_DOWN";
    public static readonly string ENTER = "KEY_ENTER";
    public static readonly string RETURN = "KEY_RETURN";
    public static readonly string EXIT = "KEY_EXIT";
    public static readonly string MENU = "KEY_MENU"; // SETTINGS button
    public static readonly string INFO = "KEY_INFO";
    public static readonly string SUB_TITLE = "KEY_SUB_TITLE"; // CC/VD button
    public static readonly string STOP = "KEY_STOP";
    public static readonly string REWIND = "KEY_REWIND";
    public static readonly string FF = "KEY_FF";
    public static readonly string PLAY = "KEY_PLAY";
    public static readonly string PAUSE = "KEY_PAUSE";
    public static readonly string RED = "KEY_RED";
    public static readonly string GREEN = "KEY_GREEN";
    public static readonly string YELLOW = "KEY_YELLOW";
    public static readonly string CYAN = "KEY_CYAN";

}