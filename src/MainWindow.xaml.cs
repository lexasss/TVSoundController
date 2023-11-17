using System.Windows;

namespace TVSoundController
{
    /// <summary>
    /// The main window that manages pages
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var connectPage = new ConnectPage();
            connectPage.Connected += (s, e) => Content = new MainPage(e);

            Content = connectPage;
        }
    }
}
