using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TVSoundController;

/// <summary>
/// The page displayed controls to specify mic and TV connection settings
/// </summary>
public partial class ConnectPage : Page
{
    public event EventHandler<Controller>? Connected;

    public ConnectPage()
    {
        InitializeComponent();

        cmbMicrophones.ItemsSource = Microphone.Devices;
        txbTVIP.Text = App.Settings.TVIP;
        txbTVMAC.Text = App.Settings.TVMAC;

        var micId = App.Settings.MicID;
        if (micId < cmbMicrophones.Items.Count)
            cmbMicrophones.SelectedIndex = micId;
    }

    // Internal

    private void CheckInput()
    {
        bool hasMic = cmbMicrophones.SelectedIndex >= 0;
        bool isValidIP = txbTVIP.Text.Split(".").Where(p => int.TryParse(p, out int v) && v > 0 && v < 255).Count() == 4;
        bool isValidMAC = txbTVMAC.Text.Split(":").Where(p => byte.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out byte v)).Count() == 6;

        if (!isValidIP)
            tblError.Text = "IP is not valid";
        else if (!isValidMAC)
            tblError.Text = "MAC is not valid";
        else if (!hasMic)
            tblError.Text = "Microphone is not selected";
        else
            tblError.Text = "";

        btnConnect.IsEnabled = hasMic && isValidIP && isValidMAC;
    }

    // UI

    private void Microphones_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CheckInput();

        App.Settings.MicID = cmbMicrophones.SelectedIndex;
        App.Settings.Save();
    }

    private void TVIP_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckInput();
    }

    private void TVMAC_TextChanged(object sender, TextChangedEventArgs e)
    {
        CheckInput();
    }

    private async void Connect_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        btnConnect.IsEnabled = false;
        tblError.Text = "";

        var settings = new Samsung.Settings(
            appName: "SamsungTV Client",
            ipAddr: txbTVIP.Text,
            macAddr: txbTVMAC.Text,
            token: App.Settings.TVToken,
            debug: true
        );

        var samsungTV = new Samsung.TV(settings);

        tblLog.Text = "Checking whether the TV is on...";
        bool isOn = await samsungTV.IsOn();

        if (!isOn)
        {
            tblLog.Text = "TV is off. Turning it on...";
            await samsungTV.TurnOn();

            tblLog.Text = "Checking whether the TV is on now...";
            isOn = await samsungTV.IsOn(2000);
        }

        if (isOn)
        {
            tblLog.Text = $"Connecting to {samsungTV.Name}...";
            var token = await samsungTV.Connect();
            if (token != null)
            {
                App.Settings.TVToken = token;
                App.Settings.TVIP = txbTVIP.Text;
                App.Settings.TVMAC = txbTVMAC.Text;
                App.Settings.Save();
            }

            tblLog.Text = "Connected!";
            await Task.Delay(1000);

            Connected?.Invoke(this, new Controller(cmbMicrophones.SelectedIndex, samsungTV));
        }
        else
        {
            tblError.Text = "Cannot connect to the TV";
            samsungTV.Dispose();
        }

        tblLog.Text = "";

        btnConnect.IsEnabled = true;
    }
}
