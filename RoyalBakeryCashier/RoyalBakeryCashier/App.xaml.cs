using Microsoft.Maui.Controls;

namespace RoyalBakeryCashier
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new Pages.LauncherPage())
            {
                BarBackgroundColor = Color.FromArgb("#1A1A1A"),
                BarTextColor = Colors.White
            };
        }
    }
}