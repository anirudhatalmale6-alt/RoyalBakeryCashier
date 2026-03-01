namespace RoyalBakeryCashier.Pages;

public partial class LauncherPage : ContentPage
{
    public LauncherPage()
    {
        InitializeComponent();
    }

    private async void OpenCashier_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CashierPage());
    }

    private async void OpenSalesman_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SalesmanPage());
    }
}
