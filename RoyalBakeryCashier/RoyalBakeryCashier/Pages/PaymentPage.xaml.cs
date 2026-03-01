using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace RoyalBakeryCashier.Pages;

public partial class PaymentPage : ContentPage
{
    private readonly StockDbContext _db;
    private Order _order;
    private decimal _total;
    private readonly int _orderId;

    private Entry _activeEntry;
    private bool _loaded = false;

    public PaymentPage(int orderId)
    {
        InitializeComponent();
        _db = new StockDbContext();
        _orderId = orderId;

        CashEntry.Focused += (s, e) => SetActiveEntry(CashEntry);
        CardEntry.Focused += (s, e) => SetActiveEntry(CardEntry);
        _activeEntry = CashEntry;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        try
        {
            _order = await Task.Run(() => _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
                .First(o => o.Id == _orderId));

            _total = _order.TotalAmount;
            TotalLabel.Text = $"LKR {_total:N2}";
            CashEntry.Text = ((int)_total).ToString();
            CardEntry.Text = "0";
            UpdateBalance();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load order.\n\n{ex.Message}", "OK");
            await Navigation.PopModalAsync();
        }
    }

    private void SetActiveEntry(Entry entry)
    {
        _activeEntry = entry;
        // Highlight the active tab
        CashTabBtn.BackgroundColor = entry == CashEntry
            ? Color.FromArgb("#2196F3")
            : Color.FromArgb("#404040");
        CardTabBtn.BackgroundColor = entry == CardEntry
            ? Color.FromArgb("#2196F3")
            : Color.FromArgb("#404040");
    }

    private void CashTab_Clicked(object sender, EventArgs e)
    {
        SetActiveEntry(CashEntry);
        CashEntry.Focus();
    }

    private void CardTab_Clicked(object sender, EventArgs e)
    {
        SetActiveEntry(CardEntry);
        CardEntry.Focus();
    }

    private void AmountChanged(object sender, TextChangedEventArgs e)
    {
        UpdateBalance();
    }

    private void UpdateBalance()
    {
        decimal cash = decimal.TryParse(CashEntry.Text, out var c) ? c : 0;
        decimal card = decimal.TryParse(CardEntry.Text, out var d) ? d : 0;
        decimal paid = cash + card;
        decimal remaining = _total - paid;

        if (remaining > 0)
        {
            // Still owes money
            BalanceFrame.IsVisible = true;
            ChangeFrame.IsVisible = false;
            BalanceLabel.Text = $"LKR {remaining:N2}";
            BalanceLabel.TextColor = Colors.OrangeRed;
            BalanceTitle.Text = "Remaining";
            ConfirmBtn.IsEnabled = false;
            ConfirmBtn.BackgroundColor = Color.FromArgb("#404040");
        }
        else if (remaining == 0)
        {
            // Exact payment
            BalanceFrame.IsVisible = true;
            ChangeFrame.IsVisible = false;
            BalanceLabel.Text = "LKR 0.00";
            BalanceLabel.TextColor = Color.FromArgb("#4CAF50");
            BalanceTitle.Text = "Balance";
            ConfirmBtn.IsEnabled = true;
            ConfirmBtn.BackgroundColor = Color.FromArgb("#4CAF50");
        }
        else
        {
            // Overpaid — show change
            BalanceFrame.IsVisible = false;
            ChangeFrame.IsVisible = true;
            ChangeLabel.Text = $"LKR {Math.Abs(remaining):N2}";
            ConfirmBtn.IsEnabled = true;
            ConfirmBtn.BackgroundColor = Color.FromArgb("#4CAF50");
        }
    }

    private void Keypad_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && _activeEntry != null && (_activeEntry.Text?.Length ?? 0) < 8)
            _activeEntry.Text = (_activeEntry.Text ?? "") + btn.Text;
    }

    private void ClearKeypad_Clicked(object sender, EventArgs e)
    {
        if (_activeEntry != null)
            _activeEntry.Text = string.Empty;
    }

    private void DeleteKeypad_Clicked(object sender, EventArgs e)
    {
        if (_activeEntry != null && !string.IsNullOrEmpty(_activeEntry.Text))
            _activeEntry.Text = _activeEntry.Text.Substring(0, _activeEntry.Text.Length - 1);
    }

    private async void Cancel_Clicked(object sender, EventArgs e)
    {
        // Delete the pending order so the cashier can go back and modify the cart
        try
        {
            _db.OrderItems.RemoveRange(_order.Items);
            _db.Orders.Remove(_order);
            await _db.SaveChangesAsync();
        }
        catch { /* best effort cleanup */ }

        await Navigation.PopModalAsync();
    }

    private async void Confirm_Clicked(object sender, EventArgs e)
    {
        decimal cash = decimal.TryParse(CashEntry.Text, out var c) ? c : 0;
        decimal card = decimal.TryParse(CardEntry.Text, out var d) ? d : 0;

        if (cash + card < _total)
        {
            await DisplayAlert("Error", "Payment not enough. Balance must be zero before confirming.", "OK");
            return;
        }

        decimal change = (cash + card) - _total;
        if (change < 0) change = 0;

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Deduct stock and GRN
            DeductStock(_order);
            DeductGRNForOrder(_order);

            // 2. Create Sale record in Sales table
            var sale = new Sale
            {
                DateTime = DateTime.Now,
                TotalAmount = _total,
                CashAmount = cash,
                CardAmount = card,
                ChangeGiven = change,
                InvoiceNumber = $"INV-{_order.Id:D5}",
                Items = _order.Items.Select(i => new SaleItem
                {
                    MenuItemId = i.MenuItemId,
                    ItemName = i.MenuItem?.Name ?? "Unknown",
                    Quantity = i.Quantity,
                    PricePerItem = i.PricePerItem,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };
            _db.Sales.Add(sale);

            // 3. Clear order data (order + items + payments)
            var payments = _db.OrderPayments.Where(p => p.OrderId == _order.Id);
            _db.OrderPayments.RemoveRange(payments);
            _db.OrderItems.RemoveRange(_order.Items);
            _db.Orders.Remove(_order);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // 4. Build receipt text and send directly to thermal printer
            string receiptText = BuildReceiptText(sale, cash, card, change);
            await PrintToThermal(receiptText);

            // Close payment popup, go back to cashier
            await Navigation.PopModalAsync();
        }
        catch (DbUpdateException dbEx)
        {
            await tx.RollbackAsync();
            string innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : "No inner details";
            await DisplayAlert("Database Error", $"Message: {dbEx.Message}\nInner: {innerMsg}", "OK");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void DeductStock(Order order)
    {
        foreach (var item in order.Items)
        {
            var stock = _db.Stocks.First(s => s.MenuItemId == item.MenuItemId);
            stock.Quantity -= item.Quantity;
        }
    }

    private void DeductGRNForOrder(Order order)
    {
        foreach (var item in order.Items)
            DeductFromGRN_FIFO(item.MenuItemId, item.Quantity);
    }

    private void DeductFromGRN_FIFO(int menuItemId, int qtyNeeded)
    {
        var grns = _db.GRNItems
            .Where(g => g.MenuItemId == menuItemId && g.CurrentQuantity > 0)
            .OrderBy(g => g.Id)
            .ToList();

        foreach (var grn in grns)
        {
            if (qtyNeeded <= 0) break;
            int deduct = Math.Min(grn.CurrentQuantity, qtyNeeded);
            grn.CurrentQuantity -= deduct;
            qtyNeeded -= deduct;
        }

        if (qtyNeeded > 0)
            throw new Exception("Insufficient GRN stock");
    }

    /// <summary>
    /// Build plain-text receipt for 3-inch (80mm) thermal printer.
    /// 42 characters per line at standard font.
    /// </summary>
    private string BuildReceiptText(Sale sale, decimal cash, decimal card, decimal change)
    {
        const int W = 42; // chars per line for 80mm thermal
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

        if (cash > 0) sb.AppendLine(Row("Cash", $"LKR {cash:N2}"));
        if (card > 0) sb.AppendLine(Row("Card", $"LKR {card:N2}"));
        sb.AppendLine(Row("Change", $"LKR {change:N2}"));
        sb.AppendLine(Line());

        sb.AppendLine(Center("Thank you for your purchase!"));
        sb.AppendLine(Center("Please come again"));
        sb.AppendLine(Line('='));
        sb.AppendLine(Center("Powered by EzyCode"));
        sb.AppendLine(Center("www.ezycode.lk"));

        return sb.ToString();
    }

    /// <summary>
    /// Send receipt directly to the Epson 3-inch thermal printer via USB.
    /// Uses ESC/POS commands. Epson printers on Windows are accessible via
    /// shared printer name or raw port. Also saves receipt locally as backup.
    /// </summary>
    private async Task PrintToThermal(string receiptText)
    {
        // Always save receipt locally as backup
        try
        {
            string receiptDir = Path.Combine(FileSystem.AppDataDirectory, "receipts");
            Directory.CreateDirectory(receiptDir);
            string filePath = Path.Combine(receiptDir, $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(filePath, receiptText);
        }
        catch { /* backup save failed, not critical */ }

        try
        {
            // Epson USB thermal printer — configurable path
            // Default: try common Epson USB printer share name
            // User can override via Preferences if the printer has a different name
            string printerPath = Preferences.Get("ThermalPrinterPath", "");

            if (string.IsNullOrEmpty(printerPath))
            {
                // Auto-detect: On Windows, Epson USB printers typically appear as
                // a shared printer. Try common names.
                var candidates = new[] { "EPSON", "EPSON TM", "Receipt", "POS" };
                foreach (var name in candidates)
                {
                    string testPath = $@"\\localhost\{name}";
                    if (File.Exists(testPath) || Directory.Exists(testPath))
                    {
                        printerPath = testPath;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(printerPath))
            {
                // ESC/POS command sequence for Epson thermal
                byte[] init = { 0x1B, 0x40 };           // ESC @ — initialize printer
                byte[] alignCenter = { 0x1B, 0x61, 0x01 }; // ESC a 1 — center align (for header)
                byte[] alignLeft = { 0x1B, 0x61, 0x00 };   // ESC a 0 — left align
                byte[] feedCut = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x41, 0x03 }; // Feed + GS V A 3 (partial cut)

                byte[] textBytes = Encoding.GetEncoding("IBM437").GetBytes(receiptText);

                using var fs = new FileStream(printerPath, FileMode.Open, FileAccess.Write);
                await fs.WriteAsync(init);
                await fs.WriteAsync(alignLeft);
                await fs.WriteAsync(textBytes);
                await fs.WriteAsync(feedCut);
                await fs.FlushAsync();
            }
            // If no printer path found, receipt is still saved locally
        }
        catch
        {
            // Print failure doesn't block the sale — it's already saved in Sales table
        }
    }
}
