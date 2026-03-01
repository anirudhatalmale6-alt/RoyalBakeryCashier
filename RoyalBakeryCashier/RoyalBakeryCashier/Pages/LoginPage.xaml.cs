using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;

namespace RoyalBakeryCashier.Pages;

public partial class LoginPage : ContentPage
{
    private readonly StockDbContext _db;

    public LoginPage()
    {
        InitializeComponent();
        _db = new StockDbContext();

        // Show mode-appropriate label
        if (App.TerminalMode == "Salesman")
            ModeLabel.Text = $"{App.TerminalName} Login";
        else if (App.TerminalMode == "Cashier")
            ModeLabel.Text = "Cashier Login";
        else
            ModeLabel.Text = "Login";
    }

    private void Password_Completed(object sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    private async void Login_Clicked(object sender, EventArgs e)
    {
        string username = (UsernameEntry.Text ?? "").Trim();
        string password = (PasswordEntry.Text ?? "").Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter username and password.");
            return;
        }

        LoginBtn.IsEnabled = false;
        LoginBtn.Text = "Logging in...";
        ErrorLabel.IsVisible = false;

        try
        {
            // Ensure DB schema is up to date (creates Users table if needed)
            await Task.Run(() =>
            {
                _db.Database.EnsureCreated();
                _db.ApplyMigrations();
            });

            var user = _db.Users.FirstOrDefault(u =>
                u.Username == username && u.IsActive);

            if (user == null || user.PasswordHash != password)
            {
                ShowError("Invalid username or password.");
                return;
            }

            // Check role matches terminal mode
            if (App.TerminalMode == "Cashier" && user.Role != "Cashier" && user.Role != "Admin")
            {
                ShowError("This account does not have cashier access.");
                return;
            }

            if (App.TerminalMode == "Salesman" && user.Role != "Salesman" && user.Role != "Admin")
            {
                ShowError("This account does not have salesman access.");
                return;
            }

            // Store logged-in user info
            App.LoggedInUserName = user.DisplayName;
            App.LoggedInUserId = user.Id;

            // Navigate to the appropriate page
            ContentPage targetPage;
            if (App.TerminalMode == "Cashier")
                targetPage = new CashierPage();
            else if (App.TerminalMode == "Salesman")
                targetPage = new SalesmanPage();
            else
                targetPage = new LauncherPage();

            // Replace entire navigation stack so user can't go back to login
            Application.Current!.MainPage = new NavigationPage(targetPage)
            {
                BarBackgroundColor = Color.FromArgb("#1A1A1A"),
                BarTextColor = Colors.White
            };
        }
        catch (Exception ex)
        {
            App.LogCrash("Login", ex);
            ShowError($"Connection error: {ex.Message}");
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Text = "Login";
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
