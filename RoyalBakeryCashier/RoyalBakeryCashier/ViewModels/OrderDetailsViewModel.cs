using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel; // MainThread

namespace RoyalBakeryCashier.ViewModels
{
    public class OrderDetailsViewModel : INotifyPropertyChanged
    {
        private Order _order;

        public event PropertyChangedEventHandler PropertyChanged;

        public OrderDetailsViewModel(Order order)
        {
            _order = order ?? throw new ArgumentNullException(nameof(order));

            // Load menu item names/prices for display
            using var db = new StockDbContext();

            var menuItemIds = (_order.Items ?? Array.Empty<OrderItem>()).Select(i => i.MenuItemId).Distinct().ToList();
            var menuItems = db.MenuItems
                              .Where(m => menuItemIds.Contains(m.Id))
                              .ToDictionary(m => m.Id);

            Items = new ObservableCollection<OrderItemDisplay>(
                (_order.Items ?? Array.Empty<OrderItem>())
                .Select(oi =>
                {
                    menuItems.TryGetValue(oi.MenuItemId, out var mi);
                    var name = mi?.Name ?? $"Item #{oi.MenuItemId}";
                    var unitPrice = mi?.Price ?? oi.PricePerItem;
                    var subTotal = oi.TotalPrice != 0m ? oi.TotalPrice : (unitPrice * oi.Quantity);
                    return new OrderItemDisplay
                    {
                        OrderItemId = oi.Id,
                        MenuItemId = oi.MenuItemId,
                        Name = name,
                        UnitPrice = unitPrice,
                        Quantity = oi.Quantity,
                        SubTotal = subTotal
                    };
                })
                .ToList()
            );

            // Totals
            TaxAmount = 0m;
            DiscountAmount = 0m;
            _totalAmount = CalculateTotal();
            UpdateGrandTotalAndEditable();

            // Commands
            AppendDigitCommand = new Command<string>(AppendDigit);
            BackspaceCommand = new Command(Backspace);
            ClearCommand = new Command(ClearInput);
            ConfirmQuantityCommand = new Command(async () => await ConfirmQuantityAsync());

            // Payment commands: the page wires up OnRequestPayment to show UI.
            // Pass an invoker that calls the async implementation.
            PayCommand = new Command(() =>
            {
                OnRequestPayment?.Invoke(ApplyPaymentResultInvoker);
            });
        }

        // callback the page will set to present payment UI; VM provides ApplyPaymentResultInvoker as the callback the page will call with the payment result
        public Action<Action<PaymentResult>> OnRequestPayment { get; set; }

        // callback the UI can subscribe to for post-payment messages (e.g. "Order completed")
        public Action<string> PaymentCompleted { get; set; }

        // Invoker compatible with the existing callback signature.
        private void ApplyPaymentResultInvoker(PaymentResult result)
        {
            // fire-and-forget the async logic; errors are caught and reported back via PaymentCompleted
            _ = ApplyPaymentResultAsync(result);
        }

        // apply result from payment modal (now persists order status + stock changes)
        private async Task ApplyPaymentResultAsync(PaymentResult result)
        {
            if (result == null)
            {
                InvokePaymentCompleted("Payment cancelled or invalid result.");
                return;
            }

            PaidAmount = result.Tender;
            ChangeAmount = result.Tender - GrandTotal;
            PaymentMethod = result.Method;

            string completionMessage = "Payment applied.";

            Debug.WriteLine($"ApplyPaymentResultAsync starting for OrderId={_order?.Id}, Tender={result.Tender}, Method={result.Method}");

            // Persist order status and reduce stocks
            try
            {
                using var db = new StockDbContext();

                // Ensure we are operating on the DB record for this order id
                var dbOrder = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == _order.Id);
                if (dbOrder == null)
                {
                    completionMessage = $"Payment applied but order id {_order?.Id} not found in database.";
                    Debug.WriteLine(completionMessage);
                    InvokePaymentCompleted(completionMessage);
                    return;
                }

                // Mark completed (0 = Completed per your model) and set final total
                dbOrder.Status = 0;
                dbOrder.TotalAmount = PaidAmount;

                // Reduce stock quantities for each order item
                foreach (var oi in dbOrder.Items)
                {
                    var stock = await db.Stocks.FirstOrDefaultAsync(s => s.MenuItemId == oi.MenuItemId);
                    if (stock != null)
                    {
                        var newQty = stock.Quantity - oi.Quantity;
                        stock.Quantity = newQty < 0 ? 0 : newQty;
                        Debug.WriteLine($"Stock updated for MenuItemId={oi.MenuItemId}: newQty={stock.Quantity}");
                    }
                    else
                    {
                        Debug.WriteLine($"No stock row found for MenuItemId={oi.MenuItemId}");
                    }
                }

                var saved = await db.SaveChangesAsync();
                Debug.WriteLine($"SaveChangesAsync returned {saved}.");

                if (saved > 0)
                {
                    completionMessage = "Payment successful — order completed and stock updated.";
                }
                else
                {
                    completionMessage = "Payment applied but no database changes were saved.";
                }

                // Reflect DB changes in in-memory order
                _order.Status = dbOrder.Status;
                _order.TotalAmount = dbOrder.TotalAmount;
                foreach (var localItem in _order.Items)
                {
                    var matchingDbItem = dbOrder.Items.FirstOrDefault(i => i.Id == localItem.Id);
                    if (matchingDbItem != null)
                    {
                        localItem.TotalPrice = matchingDbItem.TotalPrice;
                        localItem.Quantity = matchingDbItem.Quantity;
                    }
                }
            }
            catch (Exception ex)
            {
                completionMessage = "Payment applied but failed to update order: " + ex.Message;
                Debug.WriteLine("ApplyPaymentResultAsync exception: " + ex);
            }

            InvokePaymentCompleted(completionMessage);

            OnPropertyChanged(nameof(PaidAmount));
            OnPropertyChanged(nameof(ChangeAmount));
            OnPropertyChanged(nameof(PaymentMethod));
            OnPropertyChanged(nameof(GrandTotal));
        }

        // Helper to invoke PaymentCompleted on UI thread
        private void InvokePaymentCompleted(string message)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() => PaymentCompleted?.Invoke(message));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InvokePaymentCompleted failed: " + ex);
            }
        }

        // small DTO to pass result back
        public class PaymentResult
        {
            public string Method { get; set; } = string.Empty;
            public decimal Tender { get; set; }
        }

        // Exposed properties for UI
        public ObservableCollection<OrderItemDisplay> Items { get; }

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            private set
            {
                if (_totalAmount == value) return;
                _totalAmount = value;
                OnPropertyChanged();
            }
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set
            {
                if (_taxAmount == value) return;
                _taxAmount = value;
                OnPropertyChanged();
                UpdateGrandTotalAndEditable();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (_discountAmount == value) return;
                _discountAmount = value;
                OnPropertyChanged();
                UpdateGrandTotalAndEditable();
            }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            private set
            {
                if (_grandTotal == value) return;
                _grandTotal = value;
                OnPropertyChanged();
            }
        }

        private string _editableTotal = string.Empty;
        public string EditableTotal
        {
            get => _editableTotal;
            set
            {
                if (_editableTotal == value) return;
                _editableTotal = value;
                OnPropertyChanged();
            }
        }

        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            private set
            {
                if (_paidAmount == value) return;
                _paidAmount = value;
                OnPropertyChanged();
            }
        }

        private decimal _changeAmount;
        public decimal ChangeAmount
        {
            get => _changeAmount;
            private set
            {
                if (_changeAmount == value) return;
                _changeAmount = value;
                OnPropertyChanged();
            }
        }

        private string _paymentMethod = string.Empty;
        public string PaymentMethod
        {
            get => _paymentMethod;
            private set
            {
                if (_paymentMethod == value) return;
                _paymentMethod = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand AppendDigitCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ConfirmQuantityCommand { get; }
        public ICommand PayCommand { get; }

        // Selected item / input (numpad) kept unchanged...
        private OrderItemDisplay _selectedItem;
        public OrderItemDisplay SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                OnPropertyChanged();
                InputQuantity = _selectedItem?.Quantity.ToString() ?? string.Empty;
            }
        }

        private string _inputQuantity = string.Empty;
        public string InputQuantity
        {
            get => _inputQuantity;
            set
            {
                if (_inputQuantity == value) return;
                _inputQuantity = value;
                OnPropertyChanged();
            }
        }

        private void AppendDigit(string digit)
        {
            if (string.IsNullOrEmpty(digit)) return;
            if (InputQuantity == "0")
                InputQuantity = digit;
            else
                InputQuantity += digit;
        }

        private void Backspace()
        {
            if (string.IsNullOrEmpty(InputQuantity)) return;
            InputQuantity = InputQuantity.Length == 1 ? string.Empty : InputQuantity.Substring(0, InputQuantity.Length - 1);
        }

        private void ClearInput() => InputQuantity = string.Empty;

        private async Task ConfirmQuantityAsync()
        {
            if (SelectedItem == null) return;

            if (!int.TryParse(InputQuantity, out var qty) || qty < 0)
                return;

            SelectedItem.Quantity = qty;
            SelectedItem.SubTotal = SelectedItem.UnitPrice * qty;

            try
            {
                using var db = new StockDbContext();
                var dbItem = await db.OrderItems.FirstOrDefaultAsync(i => i.Id == SelectedItem.OrderItemId);
                if (dbItem != null)
                {
                    dbItem.Quantity = qty;
                    dbItem.TotalPrice = SelectedItem.SubTotal;
                    await db.SaveChangesAsync();

                    var localOrderItem = _order.Items.FirstOrDefault(i => i.Id == SelectedItem.OrderItemId);
                    if (localOrderItem != null)
                    {
                        localOrderItem.Quantity = qty;
                        localOrderItem.TotalPrice = SelectedItem.SubTotal;
                    }
                }
            }
            catch
            {
                // swallow or surface error — keep simple for now
            }

            // Recompute and notify
            TotalAmount = CalculateTotal();
            UpdateGrandTotalAndEditable();
        }

        private decimal CalculateTotal()
        {
            return Items.Sum(i => i.SubTotal);
        }

        private void UpdateGrandTotalAndEditable()
        {
            TotalAmount = CalculateTotal();
            GrandTotal = TotalAmount + TaxAmount - DiscountAmount;
            // default editable value to current grand total when viewmodel initialized or totals change,
            // but do not overwrite manually edited value if user already changed it.
            if (string.IsNullOrWhiteSpace(EditableTotal))
                EditableTotal = GrandTotal.ToString("F2");
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Lightweight display model for the CollectionView
        public class OrderItemDisplay : INotifyPropertyChanged
        {
            private int _quantity;
            private decimal _subTotal;

            public int OrderItemId { get; set; }
            public int MenuItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }

            public int Quantity
            {
                get => _quantity;
                set
                {
                    if (_quantity == value) return;
                    _quantity = value;
                    OnPropertyChanged();
                }
            }

            public decimal SubTotal
            {
                get => _subTotal;
                set
                {
                    if (_subTotal == value) return;
                    _subTotal = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
