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
    /// Styled receipt viewer — white paper with proper left-right aligned rows.
    /// Matches the original receipt design (not plain text dump).
    /// </summary>
    public class ReceiptViewPage : ContentPage
    {
        private static readonly Color Black = Colors.Black;
        private static readonly Color Grey = Color.FromArgb("#555555");
        private static readonly Color LightGrey = Color.FromArgb("#999999");

        public ReceiptViewPage(string invoiceNumber, string receiptText, Sale sale, SalesHistoryPage parent)
        {
            Title = invoiceNumber;
            BackgroundColor = Color.FromArgb("#1A1A1A");

            // Build structured receipt
            var receiptStack = new VerticalStackLayout
            {
                Spacing = 0,
                Padding = new Thickness(16, 20),
                BackgroundColor = Colors.White,
                WidthRequest = 320,
                HorizontalOptions = LayoutOptions.Center
            };

            decimal total = sale.TotalAmount;
            decimal change = (sale.CashAmount + sale.CardAmount) - total;
            if (change < 0) change = 0;

            // Header
            AddCenter(receiptStack, "The Royal Bakery", 20, true, Black);
            AddCenter(receiptStack, "202, Galle Road, Colombo-06", 11, false, Grey);
            AddLine(receiptStack, 2);

            // Invoice details
            AddRow(receiptStack, "Invoice #:", sale.InvoiceNumber, 12, Black);
            AddRow(receiptStack, "Date:", sale.DateTime.ToString("dd/MM/yyyy HH:mm"), 12, Black);
            AddRow(receiptStack, "Cashier:", sale.CashierName ?? "Cashier", 12, Black);
            AddLine(receiptStack, 1);

            // Items
            foreach (var item in sale.Items)
            {
                receiptStack.Children.Add(new Label
                {
                    Text = item.ItemName,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 12,
                    TextColor = Black,
                    Padding = new Thickness(0, 4, 0, 0)
                });
                AddRow(receiptStack, $"  {item.Quantity} x LKR {item.PricePerItem:N2}",
                    $"LKR {item.TotalPrice:N2}", 11, Grey);
            }

            AddLine(receiptStack, 1);

            // Subtotal
            AddRow(receiptStack, "Subtotal", $"LKR {total:N2}", 12, Black);
            AddLine(receiptStack, 2);

            // TOTAL
            AddRow(receiptStack, "TOTAL", $"LKR {total:N2}", 14, Black, true);
            AddLine(receiptStack, 1);

            // Payment breakdown
            if (sale.CashAmount > 0)
                AddRow(receiptStack, "Cash", $"LKR {sale.CashAmount:N2}", 12, Black);
            if (sale.CardAmount > 0)
                AddRow(receiptStack, "Card", $"LKR {sale.CardAmount:N2}", 12, Black);
            AddRow(receiptStack, "Change", $"LKR {change:N2}", 12, Black, true);
            AddLine(receiptStack, 1);

            // Footer
            AddCenter(receiptStack, "** REPRINT **", 12, true, Black);
            AddCenter(receiptStack, "Thank you for your purchase!", 11, false, Grey);
            AddCenter(receiptStack, "Please come again", 11, false, Grey);
            AddLine(receiptStack, 1);
            AddCenter(receiptStack, "Powered by EzyCode", 10, false, LightGrey);
            AddCenter(receiptStack, "www.ezycode.lk", 10, false, LightGrey);

            // Wrap in frame for paper look
            var paperFrame = new Frame
            {
                BackgroundColor = Colors.White,
                CornerRadius = 4,
                Padding = 0,
                HasShadow = true,
                BorderColor = Color.FromArgb("#CCCCCC"),
                WidthRequest = 320,
                HorizontalOptions = LayoutOptions.Center,
                Content = receiptStack
            };

            var reprintBtn = new Button
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
            };

            var backBtn = new Button
            {
                Text = "Back",
                BackgroundColor = Color.FromArgb("#757575"),
                TextColor = Colors.White,
                FontSize = 15,
                HeightRequest = 42,
                CornerRadius = 10,
                WidthRequest = 320,
                HorizontalOptions = LayoutOptions.Center
            };

            reprintBtn.Clicked += async (s, e) => await parent.ReprintSale(sale);
            backBtn.Clicked += async (s, e) => await Navigation.PopAsync();

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20),
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { paperFrame, reprintBtn, backBtn }
                }
            };
        }

        private static void AddCenter(VerticalStackLayout stack, string text, double size, bool bold, Color color)
        {
            stack.Children.Add(new Label
            {
                Text = text,
                FontSize = size,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = color,
                HorizontalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(0, 1)
            });
        }

        private static void AddRow(VerticalStackLayout stack, string left, string right,
            double size = 12, Color? color = null, bool bold = false)
        {
            color ??= Colors.Black;
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Padding = new Thickness(0, 1)
            };
            grid.Add(new Label
            {
                Text = left, FontSize = size, TextColor = color,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
            }, 0);
            grid.Add(new Label
            {
                Text = right, FontSize = size, TextColor = color,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.End
            }, 1);
            stack.Children.Add(grid);
        }

        private static void AddLine(VerticalStackLayout stack, int thickness)
        {
            stack.Children.Add(new BoxView
            {
                HeightRequest = thickness,
                Color = Color.FromArgb(thickness > 1 ? "#333333" : "#CCCCCC"),
                Margin = new Thickness(0, 6)
            });
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

        sb.AppendLine(Center("The Royal Bakery"));
        sb.AppendLine(Center("202, Galle Road, Colombo-06"));
        sb.AppendLine(Center("0112 500 991 / 0114 341 642"));
        sb.AppendLine(Center("www.theroyalbakery.com"));
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
        sb.AppendLine(Center("Powered by EzyCode"));
        sb.AppendLine(Center("www.ezycode.lk"));

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
