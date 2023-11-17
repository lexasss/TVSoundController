using System.Windows;

namespace TVSoundController;

public partial class App : Application
{
    /// <summary>
    /// Just a shortcut to <see cref="Properties.Settings.Default"/>
    /// </summary>
    internal static Properties.Settings Settings { get => TVSoundController.Properties.Settings.Default; }
}
