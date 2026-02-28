using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using RoyalBakeryCashier.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace RoyalBakeryCashier.Pages
{
    public partial class CashierPage : ContentPage
    {
        private readonly StockDbContext _dbContext;
        private ObservableCollection<ItemViewModel> _allItems;
        private ObservableCollection<ItemViewModel> _filteredItems;
        private ObservableCollection<CartItem> _cartItems;

        public CashierPage()
        {
            InitializeComponent();
            _dbContext = new StockDbContext();

            _cartItems = new ObservableCollection<CartItem>();
            CartCollectionView.ItemsSource = _cartItems;

            LoadCategories();
            LoadItems();
        }

        private void LoadCategories()
        {
            var categories = _dbContext.MenuCategories.ToList();
            CategoryStack.Children.Clear();
            CategoryStack.Children.Add(CreateCategoryButton("All", null, null));
            foreach (var cat in categories)
                CategoryStack.Children.Add(CreateCategoryButton(cat.Name, null, cat.Id));
        }

        private Button CreateCategoryButton(string name, string emoji, int? categoryId)
        {
            var btn = new Button
            {
                Text = name,
                BackgroundColor = Colors.LightBlue,
                TextColor = Colors.Black,
                CornerRadius = 8,
                FontSize = 14,
                HeightRequest = 50,
                WidthRequest = 120
            };
            btn.Clicked += (s, e) => FilterItems(categoryId);
            return btn;
        }

        private async void GoToClearance_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("ClearStock");
        }

        private void LoadItems()
        {
            var items = _dbContext.Stocks
                .Include(s => s.MenuItem)
                .Select(s => new ItemViewModel
                {
                    MenuItemId = s.MenuItemId,
                    Name = s.MenuItem.Name,
                    Price = s.MenuItem.Price,
                    AvailableStock = s.Quantity
                })
                .ToList();

            _allItems = new ObservableCollection<ItemViewModel>(items);
            _filteredItems = new ObservableCollection<ItemViewModel>(_allItems);
            ItemsCollectionView.ItemsSource = _filteredItems;
        }

        private void FilterItems(int? categoryId)
        {
            var query = _dbContext.Stocks.Include(s => s.MenuItem).AsQueryable();
            if (categoryId != null)
                query = query.Where(s => s.MenuItem.MenuCategoryId == categoryId);

            var items = query
                .Select(s => new ItemViewModel
                {
                    MenuItemId = s.MenuItemId,
                    Name = s.MenuItem.Name,
                    Price = s.MenuItem.Price,
                    AvailableStock = s.Quantity
                })
                .ToList();

            _filteredItems.Clear();
            foreach (var it in items)
                _filteredItems.Add(it);

            RefreshItemsList();
        }

        private void ItemsCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is ItemViewModel selected)
            {
                int qty = 1;
                var entered = (QuantityEntry.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(entered) && int.TryParse(entered, out int parsed) && parsed > 0)
                    qty = parsed;

                AddToCart(selected, qty);
                QuantityEntry.Text = string.Empty;
                ItemsCollectionView.SelectedItem = null;
            }
        }

        private void AddToCart(ItemViewModel menuItem, int qty)
        {
            if (qty <= 0) return;

            if (qty > menuItem.AvailableStock)
            {
                DisplayAlert("Stock", $"Only {menuItem.AvailableStock} items left for {menuItem.Name}", "OK");
                return;
            }

            var existing = _cartItems.FirstOrDefault(c => c.MenuItemId == menuItem.MenuItemId);
            if (existing != null)
            {
                if (existing.Quantity + qty > menuItem.AvailableStock)
                {
                    DisplayAlert("Stock", $"Cannot exceed available stock ({menuItem.AvailableStock})", "OK");
                    return;
                }
                existing.Quantity += qty;
                existing.Total = existing.Quantity * existing.Price;
            }
            else
            {
                _cartItems.Add(new CartItem
                {
                    MenuItemId = menuItem.MenuItemId,
                    Name = menuItem.Name,
                    Quantity = qty,
                    Price = menuItem.Price,
                    Total = qty * menuItem.Price
                });
            }

            menuItem.AvailableStock -= qty;

            UpdateTotal();
            RefreshCart();
            RefreshItemsList();
        }

        private void RefreshItemsList()
        {
            ItemsCollectionView.ItemsSource = null;
            ItemsCollectionView.ItemsSource = _filteredItems;
        }

        private void Keypad_Clicked(object sender, EventArgs e)
        {
            if (sender is Button b && (QuantityEntry.Text?.Length ?? 0) < 6)
                QuantityEntry.Text += b.Text;
        }

        private void ClearKeypad_Clicked(object sender, EventArgs e) => QuantityEntry.Text = string.Empty;

        private void ClearCart_Clicked(object sender, EventArgs e)
        {
            _cartItems.Clear();
            LoadItems();
            UpdateTotal();
            RefreshCart();
        }

        private async void PlaceOrder_Clicked(object sender, EventArgs e)
        {
            if (!_cartItems.Any())
            {
                await DisplayAlert("Info", "Cart is empty!", "OK");
                return;
            }

            var order = new Order
            {
                DateTime = DateTime.Now,
                Status = 0,
                TotalAmount = _cartItems.Sum(c => c.Total),
                Items = _cartItems.Select(c => new OrderItem
                {
                    MenuItemId = c.MenuItemId,
                    Quantity = c.Quantity,
                    PricePerItem = c.Price,
                    TotalPrice = c.Total,
                    MenuItem = null
                }).ToList()
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            await Navigation.PushAsync(new PaymentPage(order.Id));
        }

        private void DecreaseQuantity_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is CartItem item)
            {
                item.Quantity--;
                if (item.Quantity <= 0)
                    _cartItems.Remove(item);
                else
                    item.Total = item.Quantity * item.Price;

                var menuItem = _allItems.FirstOrDefault(x => x.MenuItemId == item.MenuItemId);
                if (menuItem != null)
                    menuItem.AvailableStock++;

                UpdateTotal();
                RefreshCart();
                RefreshItemsList();
            }
        }

        private void IncreaseQuantity_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is CartItem item)
            {
                var menuItem = _allItems.FirstOrDefault(x => x.MenuItemId == item.MenuItemId);
                if (menuItem != null && item.Quantity < menuItem.AvailableStock)
                {
                    item.Quantity++;
                    item.Total = item.Quantity * item.Price;
                }
                else
                {
                    DisplayAlert("Stock", $"Cannot exceed available stock ({menuItem?.AvailableStock})", "OK");
                }

                UpdateTotal();
                RefreshCart();
            }
        }

        private void UpdateTotal() => TotalLabel.Text = $"Total: LKR{_cartItems.Sum(c => c.Total):F2}";

        private void RefreshCart()
        {
            CartCollectionView.ItemsSource = null;
            CartCollectionView.ItemsSource = _cartItems;
        }

        public class CartItem
        {
            public int MenuItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal Total { get; set; }
        }

        public class ItemViewModel
        {
            public int MenuItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int AvailableStock { get; set; }
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
                {
                    sb.AppendLine($"{item.Name} x{item.Quantity} @ LKR{item.UnitPrice:F2} = LKR{item.SubTotal:F2}");
                }

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
}