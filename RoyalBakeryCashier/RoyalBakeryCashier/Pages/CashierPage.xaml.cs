using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
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

        // ===== Categories =====
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

        // ===== Items =====
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

        // ===== Selection =====
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

        // ===== Add to cart =====
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

        // ===== Keypad =====
        private void Keypad_Clicked(object sender, EventArgs e)
        {
            if (sender is Button b && (QuantityEntry.Text?.Length ?? 0) < 6)
                QuantityEntry.Text += b.Text;
        }

        private void ClearKeypad_Clicked(object sender, EventArgs e) => QuantityEntry.Text = string.Empty;

        // ===== Cart management =====
        private void ClearCart_Clicked(object sender, EventArgs e)
        {
            _cartItems.Clear();
            LoadItems();
            UpdateTotal();
            RefreshCart();
        }

        // ===== Place Order & Show Receipt =====
        private async void PlaceOrder_Clicked(object sender, EventArgs e)
        {
            if (!_cartItems.Any())
            {
                await DisplayAlert("Info", "Cart is empty!", "OK");
                return;
            }

            // Create new order
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
                    TotalPrice = c.Total
                }).ToList()
            };

            _dbContext.Orders.Add(order);
            _dbContext.SaveChanges();

            await DisplayAlert("Success", $"Order #{order.Id} placed!", "OK");

            // Show receipt page
            await Navigation.PushAsync(new ReceiptPage(order));

            // Reset POS page
            _cartItems.Clear();
            LoadItems();
            UpdateTotal();
            RefreshCart();
            QuantityEntry.Text = string.Empty;
        }

        // ===== Increase / Decrease =====
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

        // ===== Models =====
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
    }

    // ===== ReceiptPage (simple text preview) =====
    public class ReceiptPage : ContentPage
    {
        public ReceiptPage(Order order)
        {
            Title = "Receipt";
            var sb = new StringBuilder();
            sb.AppendLine("=== Royal Bakery ===");
            sb.AppendLine($"Order #: {order.Id}");
            sb.AppendLine($"Date: {order.DateTime}");
            sb.AppendLine("------------------------");
            foreach (var item in order.Items)
            {
                sb.AppendLine($"ItemId: {item.MenuItemId} x{item.Quantity} @ LKR{item.PricePerItem:F2} = LKR{item.TotalPrice:F2}");
            }
            sb.AppendLine("------------------------");
            sb.AppendLine($"Total: LKR{order.TotalAmount:F2}");
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