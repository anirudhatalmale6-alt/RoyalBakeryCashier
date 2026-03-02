using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using RoyalBakeryCashier.Helpers;
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
                CashierName = string.IsNullOrEmpty(App.LoggedInUserName) ? "Cashier" : App.LoggedInUserName,
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

            // 4. Print receipt directly to thermal printer
            await PrintToThermal(sale, cash, card, change);

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
    /// Send receipt to 3-inch (80mm) Epson thermal printer using ESC/POS commands.
    /// Uses printer-native center alignment for headers/footers (no space padding).
    /// </summary>
    private async Task PrintToThermal(Sale sale, decimal cash, decimal card, decimal change)
    {
        const int W = 48;
        string Separator(char c = '-') => new string(c, W);
        string Row(string left, string right) => left + right.PadLeft(W - left.Length);

        // Save receipt as text backup
        try
        {
            string receiptDir = Path.Combine(FileSystem.AppDataDirectory, "receipts");
            Directory.CreateDirectory(receiptDir);
            await File.WriteAllTextAsync(
                Path.Combine(receiptDir, $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),
                $"Invoice: {sale.InvoiceNumber} | Total: LKR {sale.TotalAmount:N2}");
        }
        catch { }

        try
        {
            // Find the printer: check saved preference first, then auto-detect
            string printerName = Preferences.Get("ThermalPrinterName", "");
            if (string.IsNullOrEmpty(printerName))
            {
                printerName = RawPrinterHelper.FindThermalPrinter() ?? "";
                if (!string.IsNullOrEmpty(printerName))
                    Preferences.Set("ThermalPrinterName", printerName);
            }

            if (string.IsNullOrEmpty(printerName))
            {
                // Show available printers so user knows what to pick
                var printers = RawPrinterHelper.GetInstalledPrinters();
                string msg = printers.Count > 0
                    ? $"No thermal printer detected.\n\nInstalled printers:\n{string.Join("\n", printers)}\n\nPlease set printer name in Settings."
                    : "No printers found. Please install the Epson TM-T82 driver.";
                await DisplayAlert("Printer Not Found", msg, "OK");
                return;
            }

            var enc = Encoding.GetEncoding("IBM437");
            byte[] init = { 0x1B, 0x40 };
            byte[] center = { 0x1B, 0x61, 0x01 };
            byte[] left = { 0x1B, 0x61, 0x00 };
            byte[] feedCut = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x41, 0x03 };

            using var ms = new MemoryStream();

            ms.Write(init);

            // Header — centered
            ms.Write(center);
            ms.Write(enc.GetBytes("The Royal Bakery\n"));
            ms.Write(enc.GetBytes("202, Galle Road, Colombo-06\n"));
            ms.Write(enc.GetBytes("0112 500 991 / 0114 341 642\n"));
            ms.Write(enc.GetBytes("www.theroyalbakery.com\n"));
            ms.Write(enc.GetBytes(Separator('=') + "\n"));

            // Body — left aligned
            ms.Write(left);
            ms.Write(enc.GetBytes(Row("Invoice #:", sale.InvoiceNumber) + "\n"));
            ms.Write(enc.GetBytes(Row("Date:", sale.DateTime.ToString("dd/MM/yyyy HH:mm")) + "\n"));
            ms.Write(enc.GetBytes(Row("Cashier:", sale.CashierName ?? "Cashier") + "\n"));
            ms.Write(enc.GetBytes(Separator() + "\n"));

            foreach (var item in sale.Items)
            {
                ms.Write(enc.GetBytes(item.ItemName + "\n"));
                ms.Write(enc.GetBytes(Row($"  {item.Quantity} x LKR {item.PricePerItem:N2}", $"LKR {item.TotalPrice:N2}") + "\n"));
            }

            ms.Write(enc.GetBytes(Separator() + "\n"));
            ms.Write(enc.GetBytes(Row("Subtotal", $"LKR {sale.TotalAmount:N2}") + "\n"));
            ms.Write(enc.GetBytes(Separator('=') + "\n"));
            ms.Write(enc.GetBytes(Row("TOTAL", $"LKR {sale.TotalAmount:N2}") + "\n"));
            ms.Write(enc.GetBytes(Separator() + "\n"));

            if (cash > 0) ms.Write(enc.GetBytes(Row("Cash", $"LKR {cash:N2}") + "\n"));
            if (card > 0) ms.Write(enc.GetBytes(Row("Card", $"LKR {card:N2}") + "\n"));
            ms.Write(enc.GetBytes(Row("Change", $"LKR {change:N2}") + "\n"));
            ms.Write(enc.GetBytes(Separator() + "\n"));

            // Footer — centered
            ms.Write(center);
            ms.Write(enc.GetBytes("Thank you for your purchase!\n"));
            ms.Write(enc.GetBytes("Please come again\n"));
            ms.Write(enc.GetBytes(Separator('=') + "\n"));
            ms.Write(enc.GetBytes("Powered by EzyCode\n"));
            ms.Write(enc.GetBytes("www.ezycode.lk\n"));

            ms.Write(feedCut);

            // Send raw bytes to printer via Windows Print Spooler API
            bool printed = RawPrinterHelper.SendBytesToPrinter(printerName, ms.ToArray());
            if (!printed)
            {
                await DisplayAlert("Print Error",
                    $"Failed to send data to printer: {printerName}\nPlease check the printer is on and connected.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Print Error", $"Could not print: {ex.Message}", "OK");
        }
    }
}
