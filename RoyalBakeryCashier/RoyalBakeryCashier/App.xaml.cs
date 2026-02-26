using Microsoft.Maui.Controls;

namespace RoyalBakeryCashier
{
    public partial class App : Application
    {
        public App(Pages.CashierPage startPage)
        {
            InitializeComponent();
            MainPage = new NavigationPage(startPage);
        }
    }
}