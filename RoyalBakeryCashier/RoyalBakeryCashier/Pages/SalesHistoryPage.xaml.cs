using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace RoyalBakeryCashier.Pages;

public partial class SalesHistoryPage : ContentPage
{
    private readonly StockDbContext _db;
    private List<SaleViewModel> _sales;

    public SalesHistoryPage()
    {
        InitializeComponent();
        _db = new StockDbContext();
        LoadSales();
    }

    private void LoadSales()
    {
        _sales = _db.Sales
            .Include(s => s.Items)
            .OrderByDescending(s => s.DateTime)
            .Take(50) // Last 50 sales
            .Select(s => new SaleViewModel
            {
                SaleId = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                DateTime = s.DateTime,
                TotalAmount = s.TotalAmount,
                CashAmount = s.CashAmount,
                CardAmount = s.CardAmount,
                ChangeGiven = s.ChangeGiven,
                ItemCount = s.Items.Count,
                ItemNames = string.Join(", ", s.Items.Select(i => i.ItemName))
            })
            .ToList();

        SalesListView.ItemsSource = _sales;
        CountLabel.Text = $"({_sales.Count} recent)";
    }

    private async void View_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is int saleId)
        {
            var sale = _db.Sales.Include(s => s.Items).FirstOrDefault(s => s.Id == saleId);
            if (sale == null) return;

            string receipt = BuildReceiptText(sale);

            // Show receipt on white background like real thermal paper
            await Navigation.PushAsync(new ReceiptViewPage(sale.InvoiceNumber, receipt, sale, this));
        }
    }

    /// <summary>
    /// White receipt viewer — looks like actual thermal receipt paper
    /// </summary>
    public class ReceiptViewPage : ContentPage
    {
        public ReceiptViewPage(string invoiceNumber, string receiptText, Sale sale, SalesHistoryPage parent)
        {
            Title = invoiceNumber;
            BackgroundColor = Color.FromArgb("#1A1A1A");

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20),
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        // White receipt paper
                        new Frame
                        {
                            BackgroundColor = Colors.White,
                            CornerRadius = 4,
                            Padding = new Thickness(16, 20),
                            WidthRequest = 320,
                            HasShadow = true,
                            BorderColor = Color.FromArgb("#CCCCCC"),
                            Content = new Label
                            {
                                Text = receiptText,
                                FontFamily = "Courier New",
                                FontSize = 11,
                                TextColor = Colors.Black,
                                LineHeight = 1.4,
                            }
                        },

                        // Reprint button
                        new Button
                        {
                            Text = "Reprint",
                            BackgroundColor = Color.FromArgb("#9C27B0"),
                            TextColor = Colors.White,
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            HeightRequest = 46,
                            CornerRadius = 10,
                            WidthRequest = 320,
                            HorizontalOptions = LayoutOptions.Center
                        },

                        // Back button
                        new Button
                        {
                            Text = "Back",
                            BackgroundColor = Color.FromArgb("#757575"),
                            TextColor = Colors.White,
                            FontSize = 15,
                            HeightRequest = 42,
                            CornerRadius = 10,
                            WidthRequest = 320,
                            HorizontalOptions = LayoutOptions.Center
                        }
                    }
                }
            };

            // Wire up buttons
            var stack = (VerticalStackLayout)((ScrollView)Content).Content;
            var reprintBtn = (Button)stack.Children[1];
            var backBtn = (Button)stack.Children[2];

            reprintBtn.Clicked += async (s, e) =>
            {
                await parent.ReprintSale(sale);
            };

            backBtn.Clicked += async (s, e) =>
            {
                await Navigation.PopAsync();
            };
        }
    }

    public async Task ReprintSale(Sale sale)
    {
        string receipt = BuildReceiptText(sale);
        await PrintToThermal(receipt);
        await DisplayAlert("Reprint", $"{sale.InvoiceNumber} sent to printer.", "OK");
    }

    private async void Reprint_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is int saleId)
        {
            var sale = _db.Sales.Include(s => s.Items).FirstOrDefault(s => s.Id == saleId);
            if (sale == null) return;

            await ReprintSale(sale);
        }
    }

    private async void Close_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    /// <summary>
    /// Build plain-text receipt for 3-inch (80mm) thermal printer.
    /// Same format as PaymentPage uses.
    /// </summary>
    private string BuildReceiptText(Sale sale)
    {
        const int W = 42;
        var sb = new StringBuilder();

        string Line(char c = '-') => new string(c, W);
        string Center(string s) => s.PadLeft((W + s.Length) / 2).PadRight(W);
        string Row(string left, string right) => left + right.PadLeft(W - left.Length);

        sb.AppendLine(Center("Royal Bakery"));
        sb.AppendLine(Center("123 Main Street, Colombo"));
        sb.AppendLine(Center("Tel: +94 11 234 5678"));
        sb.AppendLine(Line('='));

        sb.AppendLine(Row("Invoice #:", sale.InvoiceNumber));
        sb.AppendLine(Row("Date:", sale.DateTime.ToString("dd/MM/yyyy HH:mm")));
        sb.AppendLine(Row("Cashier:", sale.CashierName ?? "Cashier"));
        sb.AppendLine(Line());

        foreach (var item in sale.Items)
        {
            sb.AppendLine(item.ItemName);
            sb.AppendLine(Row($"  {item.Quantity} x LKR {item.PricePerItem:N2}", $"LKR {item.TotalPrice:N2}"));
        }

        sb.AppendLine(Line());
        sb.AppendLine(Row("Subtotal", $"LKR {sale.TotalAmount:N2}"));
        sb.AppendLine(Line('='));
        sb.AppendLine(Row("TOTAL", $"LKR {sale.TotalAmount:N2}"));
        sb.AppendLine(Line());

        if (sale.CashAmount > 0) sb.AppendLine(Row("Cash", $"LKR {sale.CashAmount:N2}"));
        if (sale.CardAmount > 0) sb.AppendLine(Row("Card", $"LKR {sale.CardAmount:N2}"));
        sb.AppendLine(Row("Change", $"LKR {sale.ChangeGiven:N2}"));
        sb.AppendLine(Line());

        sb.AppendLine(Center("** REPRINT **"));
        sb.AppendLine(Center("Thank you for your purchase!"));
        sb.AppendLine(Center("Please come again"));
        sb.AppendLine(Line('='));
        sb.AppendLine(Center("Powered by Royal Bakery POS"));

        return sb.ToString();
    }

    /// <summary>
    /// Send to Epson thermal printer (same logic as PaymentPage).
    /// </summary>
    private async Task PrintToThermal(string receiptText)
    {
        try
        {
            string printerPath = Preferences.Get("ThermalPrinterPath", "");

            if (!string.IsNullOrEmpty(printerPath))
            {
                byte[] init = { 0x1B, 0x40 };
                byte[] alignLeft = { 0x1B, 0x61, 0x00 };
                byte[] feedCut = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x41, 0x03 };
                byte[] textBytes = Encoding.GetEncoding("IBM437").GetBytes(receiptText);

                using var fs = new FileStream(printerPath, FileMode.Open, FileAccess.Write);
                await fs.WriteAsync(init);
                await fs.WriteAsync(alignLeft);
                await fs.WriteAsync(textBytes);
                await fs.WriteAsync(feedCut);
                await fs.FlushAsync();
            }
        }
        catch
        {
            await DisplayAlert("Print Error", "Could not reach the printer. Please check the connection.", "OK");
        }
    }

    public class SaleViewModel
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal ChangeGiven { get; set; }
        public int ItemCount { get; set; }
        public string ItemNames { get; set; } = string.Empty;

        public string DateTimeFormatted => DateTime.ToString("dd MMM yyyy, hh:mm tt");
        public string TotalFormatted => $"LKR {TotalAmount:N2}";
        public string ItemsSummary => $"{ItemCount} item(s): {ItemNames}";
        public string PaymentMethod
        {
            get
            {
                if (CashAmount > 0 && CardAmount > 0) return "Cash + Card";
                if (CardAmount > 0) return "Card";
                return "Cash";
            }
        }
    }
}
