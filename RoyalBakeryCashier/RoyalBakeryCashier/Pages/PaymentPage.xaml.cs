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

        TotalLabel.Text = $"Total: LKR {_total:F2}";
        MethodPicker.SelectedIndex = 0;
        CashEntry.Text = ((int)_total).ToString();

        CashEntry.Focused += (s, e) => _activeEntry = CashEntry;
        CardEntry.Focused += (s, e) => _activeEntry = CardEntry;

        _activeEntry = CashEntry;
    }

    private void MethodChanged(object sender, EventArgs e)
    {
        var type = MethodPicker.SelectedItem?.ToString();
        if (type == "Both")
        {
            CardEntry.IsVisible = true;
            CashEntry.Text = "0";
            CardEntry.Text = ((int)_total).ToString();
        }
        else
        {
            CardEntry.IsVisible = false;
            CashEntry.Text = ((int)_total).ToString();
        }
    }

    private void CashChanged(object sender, TextChangedEventArgs e)
    {
        if (!CardEntry.IsVisible) return;
        if (int.TryParse(CashEntry.Text, out var cash))
            CardEntry.Text = Math.Max(0, (int)_total - cash).ToString();
    }

    private void CardChanged(object sender, TextChangedEventArgs e)
    {
        if (!CardEntry.IsVisible) return;
        if (int.TryParse(CardEntry.Text, out var card))
            CashEntry.Text = Math.Max(0, (int)_total - card).ToString();
    }

    private void Keypad_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && _activeEntry != null && _activeEntry.Text.Length < 6)
            _activeEntry.Text += btn.Text;
    }

    private void ClearKeypad_Clicked(object sender, EventArgs e) => _activeEntry.Text = string.Empty;

    private void DeleteKeypad_Clicked(object sender, EventArgs e)
    {
        if (_activeEntry != null && _activeEntry.Text.Length > 0)
            _activeEntry.Text = _activeEntry.Text.Substring(0, _activeEntry.Text.Length - 1);
    }

    private async void Confirm_Clicked(object sender, EventArgs e)
    {
        int cash = int.TryParse(CashEntry.Text, out var c) ? c : 0;
        int card = int.TryParse(CardEntry.Text, out var d) ? d : 0;

        if (cash + card < (int)_total)
        {
            await DisplayAlert("Error", "Payment not enough", "OK");
            return;
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            SavePayments(cash, card);
            DeductStock(_order);
            DeductGRNForOrder(_order);

            CreatedInvoice = CreateInvoice(_order);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

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

    private void SavePayments(int cash, int card)
    {
        if (cash > 0)
            _db.OrderPayments.Add(new OrderPayments { OrderId = _order.Id, PaymentType = 0, TenderAmount = cash, DateTime = DateTime.Now });
        if (card > 0)
            _db.OrderPayments.Add(new OrderPayments { OrderId = _order.Id, PaymentType = 1, TenderAmount = card, DateTime = DateTime.Now });
    }

    private Invoice CreateInvoice(Order order)
    {
        return new Invoice
        {
            OrderId = order.Id.ToString(),
            CustomerName = "Walk-in",
            CreatedAt = DateTime.Now,
            Items = order.Items.Select(i => new InvoiceItem
            {
                Name = i.MenuItem!.Name,
                Quantity = i.Quantity,
                UnitPrice = i.PricePerItem
            }).ToList()
        };
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

    public class ReceiptPage : ContentPage
    {
        public ReceiptPage(Invoice invoice)
        {
            Title = "Receipt";
            var sb = new StringBuilder();
            sb.AppendLine("=== Royal Bakery ===");
            sb.AppendLine($"Invoice #: {invoice.OrderId}");
            sb.AppendLine($"Customer: {invoice.CustomerName}");
            sb.AppendLine($"Date: {invoice.CreatedAt}");
            sb.AppendLine("------------------------");

            foreach (var item in invoice.Items)
                sb.AppendLine($"{item.Name} x{item.Quantity} @ LKR{item.UnitPrice:F2} = LKR{item.SubTotal:F2}");

            sb.AppendLine("------------------------");
            sb.AppendLine($"Total: LKR{invoice.Items.Sum(i => i.SubTotal):F2}");
            sb.AppendLine("========================");

            Content = new ScrollView
            {
                Content = new Label
                {
                    Text = sb.ToString(),
                    FontFamily = "Consolas",
                    FontSize = 16,
                    LineHeight = 1.2
                }
            };
        }
    }
}