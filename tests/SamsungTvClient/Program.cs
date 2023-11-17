using SamsungTV;

string TokenFileName = "token.txt";

string? token = null;
try
{
    using var reader = new StreamReader(TokenFileName);
    token = reader.ReadToEnd().Trim();
}
catch { }


var settings = new Settings(
    appName: "SamsungTV Client",
    ipAddr: "192.168.1.58",
    subnet: "255.255.255.255",
    macAddr: "70-09-71-95-85-58",
    token: token,
    debug: true
); 

using var samsungTV = new SamsungTV.SamsungTV(settings);

bool isOn = await samsungTV.IsOn();
if (!isOn)
{
    await samsungTV.TurnOn();
    isOn = await samsungTV.IsOn(2000);
}

if (isOn)
{
    token = await samsungTV.Connect();
    if (token != null)
    {
        try
        {
            using var writer = new StreamWriter(TokenFileName);
            writer.Write(token);
        }
        catch { }
    }

    await Task.Delay(1000);
    await samsungTV.Press(Keys.VOLUP);
    //await samsungTV.Type("https://github.com/");
    await Task.Delay(1000);
    //await samsungTV.Press(Keys.POWER);
}
else
{
    Console.WriteLine("Failed to turn the TV on. Exiting . . .");
}

samsungTV.Dispose();

/*
int delay = 2000;
Console.WriteLine($"Waiting {delay} seconds for TV to power up...");
if (remote.IsTvOn(delay)) // Optional parameter for IsTvOn([int delay=0]) sets wait time in milliseconds before sending request
{
    string msg = settings.Token == null ? "Connecting to TV... Check for prompt to accept new connection" : "Connecting to TV...";
    Console.WriteLine(msg);
    remote.Connect();
    Console.WriteLine("Pressing volume down");
    remote.Press(Keys.VOLDOWN);
}

Console.WriteLine($"Pressing volume up after {delay} seconds");
Task.Delay(delay).Wait();
// If TV
remote.Press(Keys.VOLUP);

Console.WriteLine("Enter key code e.g 'KEY_VOLDOWN' or enter multiple key codes separated by ; character e.g. 'KEY_2;KEY_PLUS100;KEY_1'\nEnter 'exit' to end program");
while (true)
{
    string? inputKey = Console.ReadLine();
    if (inputKey == null) continue;
    if (inputKey.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
    string[] inputKeys = inputKey.Contains(';') ? inputKey.Split(';') : new string[] { inputKey };
    for (int i = 0; i < inputKeys.Length; i++)
    {
        string key = inputKeys[i].Trim();
        if (IsValidKeycode(key))
        {
            remote.Press(key);
            Task.Delay(200).Wait(); // Need delay between sending key press or will not register later command
        }
    }
}

// Turn off TV
//remote.Press(Keys.POWER);

// After first token generation, new token value is saved in settings.Token
// When SamsungRemote constructor is called on next launch with token that is already paired, TV will not prompt user to accept connection again
Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
config.AppSettings.Settings["Token"].Value = settings.Token;
config.Save(ConfigurationSaveMode.Minimal);
Console.WriteLine("Demo end");




void GenerateKeylist()
{
    Console.WriteLine("Key list:");
    Type t = typeof(Keys);
    MemberInfo[] memberInfo = t.GetMembers();
    string[] exceptionList = "GetType ToString Equals GetHashCode 0 1 2 3 4 5 6 7 8 9 POWER POWEROFF SOURCE PLUS100 PRECH VOLUP VOLDOWN MUTE CH_LIST CHDOWN CHUP HOME W_LINK GUIDE LEFT UP RIGHT DOWN ENTER RETURN EXIT SETTINGS INFO SUB_TITLE STOP REWIND FF PLAY PAUSE".Split(' ');
    foreach (MemberInfo member in memberInfo)
    {
        string name = member.Name.Replace("get_", "KEY_");
        if (name.Contains("NUM")) name = name.Replace("NUM", "");
        if (!exceptionList.Contains(name))
        {
            keyList.Add(name);
            Console.Write($"{name} ");
        }
    }
    Console.WriteLine(Environment.NewLine);
}

bool IsValidKeycode(string inputKey)
{
    if (keyList.Contains(inputKey)) return true;
    Console.WriteLine("Key invalid");
    return false;
}
*/