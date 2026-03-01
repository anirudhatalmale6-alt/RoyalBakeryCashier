namespace RoyalBakeryCashier.Pages;

public partial class LauncherPage : ContentPage
{
    public LauncherPage()
    {
        InitializeComponent();
    }

    private async void OpenCashier_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new CashierPage());
        }
        catch (Exception ex)
        {
            App.LogCrash("OpenCashier", ex);
            await DisplayAlert("Error", $"Could not open Cashier.\n\n{ex.Message}", "OK");
        }
    }

    private async void OpenSalesman_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new SalesmanPage());
        }
        catch (Exception ex)
        {
            App.LogCrash("OpenSalesman", ex);
            await DisplayAlert("Error", $"Could not open Salesman.\n\n{ex.Message}", "OK");
        }
    }

    private async void ViewCrashLog_Tapped(object sender, TappedEventArgs e)
    {
        try
        {
            string logPath = App.CrashLogPath;
            if (File.Exists(logPath))
            {
                string content = await File.ReadAllTextAsync(logPath);
                if (content.Length > 2000)
                    content = content.Substring(content.Length - 2000);
                await DisplayAlert("Crash Log", content, "OK");
            }
            else
            {
                await DisplayAlert("Crash Log", "No crashes recorded.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
