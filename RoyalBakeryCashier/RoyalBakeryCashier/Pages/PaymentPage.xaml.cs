using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using RoyalBakeryCashier.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace RoyalBakeryCashier.Pages;

public partial class PaymentPage : ContentPage
{
    private readonly StockDbContext _db;
    private readonly Order _order;
    private readonly decimal _total;

    private Entry _activeEntry;

    public Invoice? CreatedInvoice { get; private set; }

    public PaymentPage(int orderId)
    {
        InitializeComponent();
        _db = new StockDbContext();

        _order = _db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.MenuItem)
            .First(o => o.Id == orderId);

        _total = _order.TotalAmount;

        TotalLabel.Text = $"LKR {_total:N2}";

        // Default: full amount in cash
        CashEntry.Text = ((int)_total).ToString();
        CardEntry.Text = "0";

        CashEntry.Focused += (s, e) => SetActiveEntry(CashEntry);
        CardEntry.Focused += (s, e) => SetActiveEntry(CardEntry);

        _activeEntry = CashEntry;
        UpdateBalance();
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

            // 4. Build invoice DTO for receipt display
            CreatedInvoice = new Invoice
            {
                OrderId = sale.InvoiceNumber,
                CustomerName = "Walk-in",
                CreatedAt = sale.DateTime,
                CashAmount = cash,
                CardAmount = card,
                Items = sale.Items.Select(si => new InvoiceItem
                {
                    Name = si.ItemName,
                    Quantity = si.Quantity,
                    UnitPrice = si.PricePerItem
                }).ToList()
            };

            // Show receipt
            await Navigation.PushAsync(new ReceiptPage(CreatedInvoice));
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
    /// 3-inch thermal receipt page — 80mm wide, monospace font, dashed separators
    /// </summary>
    public class ReceiptPage : ContentPage
    {
        public ReceiptPage(Invoice invoice)
        {
            Title = "Receipt";
            BackgroundColor = Color.FromArgb("#1A1A1A");

            decimal total = invoice.Items.Sum(i => i.SubTotal);
            decimal cash = invoice.CashAmount;
            decimal card = invoice.CardAmount;
            decimal change = (cash + card) - total;
            if (change < 0) change = 0;

            // Build the receipt layout to mimic 3-inch thermal
            var receiptStack = new VerticalStackLayout
            {
                Spacing = 0,
                Padding = new Thickness(16, 20),
                WidthRequest = 302,
                BackgroundColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center
            };

            // Header
            receiptStack.Children.Add(CreateCenterLabel("Royal Bakery", 18, true, Colors.Black));
            receiptStack.Children.Add(CreateCenterLabel("123 Main Street, Colombo", 11, false, Color.FromArgb("#555555")));
            receiptStack.Children.Add(CreateCenterLabel("Tel: +94 11 234 5678", 11, false, Color.FromArgb("#555555")));

            receiptStack.Children.Add(CreateSolidLine());

            // Invoice details
            receiptStack.Children.Add(CreateRow("Invoice #:", $"INV-{invoice.OrderId.PadLeft(5, '0')}"));
            receiptStack.Children.Add(CreateRow("Date:", invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm")));
            receiptStack.Children.Add(CreateRow("Customer:", invoice.CustomerName));

            receiptStack.Children.Add(CreateDashedLine());

            // Items
            foreach (var item in invoice.Items)
            {
                receiptStack.Children.Add(new Label
                {
                    Text = item.Name,
                    FontFamily = "Courier New",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Black,
                    Padding = new Thickness(0, 2, 0, 0)
                });

                receiptStack.Children.Add(CreateRow(
                    $"  {item.Quantity} x LKR {item.UnitPrice:N2}",
                    $"LKR {item.SubTotal:N2}",
                    11, Color.FromArgb("#555555")));
            }

            receiptStack.Children.Add(CreateDashedLine());

            // Subtotal / Tax / Discount
            receiptStack.Children.Add(CreateRow("Subtotal", $"LKR {total:N2}"));
            receiptStack.Children.Add(CreateRow("Tax (0%)", "LKR 0.00"));
            receiptStack.Children.Add(CreateRow("Discount", "LKR 0.00"));

            receiptStack.Children.Add(CreateSolidLine());

            // TOTAL
            receiptStack.Children.Add(CreateRow("TOTAL", $"LKR {total:N2}", 14, Colors.Black, true));

            receiptStack.Children.Add(CreateDashedLine());

            // Payment breakdown
            if (cash > 0)
                receiptStack.Children.Add(CreateRow("Cash", $"LKR {cash:N2}"));
            if (card > 0)
                receiptStack.Children.Add(CreateRow("Card", $"LKR {card:N2}"));
            receiptStack.Children.Add(CreateRow("Change", $"LKR {change:N2}", 12, Colors.Black, true));

            receiptStack.Children.Add(CreateDashedLine());

            // Footer
            receiptStack.Children.Add(CreateCenterLabel("Thank you for your purchase!", 11, false, Color.FromArgb("#555555")));
            receiptStack.Children.Add(CreateCenterLabel("Please come again", 11, false, Color.FromArgb("#555555")));

            receiptStack.Children.Add(CreateDashedLine());
            receiptStack.Children.Add(CreateCenterLabel("Powered by Royal Bakery POS", 10, false, Color.FromArgb("#999999")));

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(20),
                    Spacing = 16,
                    Children =
                    {
                        receiptStack,
                        new Button
                        {
                            Text = "Print Receipt",
                            BackgroundColor = Color.FromArgb("#4CAF50"),
                            TextColor = Colors.White,
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            HeightRequest = 50,
                            CornerRadius = 10,
                            HorizontalOptions = LayoutOptions.Center,
                            WidthRequest = 302
                        },
                        new Button
                        {
                            Text = "Done",
                            BackgroundColor = Color.FromArgb("#757575"),
                            TextColor = Colors.White,
                            FontSize = 16,
                            HeightRequest = 44,
                            CornerRadius = 10,
                            HorizontalOptions = LayoutOptions.Center,
                            WidthRequest = 302
                        }
                    }
                }
            };

            // Wire up Done button to close back to cashier
            var doneBtn = ((VerticalStackLayout)((ScrollView)Content).Content).Children[2] as Button;
            if (doneBtn != null)
                doneBtn.Clicked += async (s, e) => await Navigation.PopModalAsync();
        }

        private static Label CreateCenterLabel(string text, double fontSize, bool bold, Color color)
        {
            return new Label
            {
                Text = text,
                FontFamily = "Courier New",
                FontSize = fontSize,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = color,
                HorizontalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(0, 1)
            };
        }

        private static Grid CreateRow(string left, string right, double fontSize = 12, Color? color = null, bool bold = false)
        {
            color ??= Colors.Black;
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Padding = new Thickness(0, 1)
            };
            grid.Add(new Label
            {
                Text = left,
                FontFamily = "Courier New",
                FontSize = fontSize,
                TextColor = color,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
            }, 0);
            grid.Add(new Label
            {
                Text = right,
                FontFamily = "Courier New",
                FontSize = fontSize,
                TextColor = color,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.End
            }, 1);
            return grid;
        }

        private static BoxView CreateDashedLine()
        {
            return new BoxView
            {
                HeightRequest = 1,
                Color = Color.FromArgb("#333333"),
                Margin = new Thickness(0, 6)
            };
        }

        private static BoxView CreateSolidLine()
        {
            return new BoxView
            {
                HeightRequest = 2,
                Color = Color.FromArgb("#333333"),
                Margin = new Thickness(0, 6)
            };
        }
    }
}
