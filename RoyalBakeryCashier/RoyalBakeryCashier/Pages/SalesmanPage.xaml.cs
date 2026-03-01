using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using System.Collections.ObjectModel;
using System.Text;

namespace RoyalBakeryCashier.Pages;

public partial class SalesmanPage : ContentPage
{
    private readonly StockDbContext _dbContext;
    private ObservableCollection<ItemViewModel> _allItems;
    private ObservableCollection<ItemViewModel> _filteredItems;
    private ObservableCollection<CartItem> _cartItems;

    public SalesmanPage()
    {
        InitializeComponent();
        _dbContext = new StockDbContext();
        _cartItems = new ObservableCollection<CartItem>();
        CartCollectionView.ItemsSource = _cartItems;

        try
        {
            _dbContext.Database.EnsureCreated();
            LoadCategories();
            LoadItems();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await DisplayAlert("Database Error",
                    $"Could not connect to SQL Server.\n\nMake sure SQL Server Express is running and the database exists.\n\nError: {ex.Message}",
                    "OK"));
        }
    }

    // Category colors (same as cashier)
    private static readonly Color[] _categoryColors = new[]
    {
        Color.FromArgb("#607D8B"),
        Color.FromArgb("#2196F3"),
        Color.FromArgb("#9C27B0"),
        Color.FromArgb("#FF9800"),
        Color.FromArgb("#4CAF50"),
        Color.FromArgb("#F44336"),
    };

    private const int QUICK_CATEGORY_ID = -1; // special ID for Quicks

    private void LoadCategories()
    {
        var categories = _dbContext.MenuCategories.ToList();
        CategoryGrid.Children.Clear();
        CategoryGrid.RowDefinitions.Clear();

        var allButtons = new List<(string Name, int? CatId)>
        {
            ("Quicks", QUICK_CATEGORY_ID),
            ("All", null)
        };
        foreach (var cat in categories)
            allButtons.Add((cat.Name, cat.Id));

        int cols = 5;
        int rows = (int)Math.Ceiling(allButtons.Count / (double)cols);
        for (int r = 0; r < rows; r++)
            CategoryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < allButtons.Count; i++)
        {
            var (name, catId) = allButtons[i];
            var btn = new Button
            {
                Text = name,
                BackgroundColor = i == 0 ? Color.FromArgb("#E91E63") : _categoryColors[i % _categoryColors.Length],
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 14,
                HeightRequest = 50,
            };
            btn.Clicked += (s, e) => FilterItems(catId);
            Grid.SetRow(btn, i / cols);
            Grid.SetColumn(btn, i % cols);
            CategoryGrid.Children.Add(btn);
        }
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
        if (categoryId == QUICK_CATEGORY_ID)
            query = query.Where(s => s.MenuItem.IsQuick);
        else if (categoryId != null)
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

    private async void CartItemName_Tapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is CartItem item)
        {
            bool confirm = await DisplayAlert("Remove Item",
                $"Remove \"{item.Name}\" from order?", "Delete", "Cancel");
            if (confirm)
            {
                var menuItem = _allItems.FirstOrDefault(x => x.MenuItemId == item.MenuItemId);
                if (menuItem != null) menuItem.AvailableStock += item.Quantity;
                _cartItems.Remove(item);
                UpdateTotal();
                RefreshCart();
                RefreshItemsList();
            }
        }
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
            if (menuItem != null) menuItem.AvailableStock++;

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

    // ===== CREATE SALES ORDER =====
    private async void CreateOrder_Clicked(object sender, EventArgs e)
    {
        if (!_cartItems.Any())
        {
            await DisplayAlert("Info", "Add items to create an order.", "OK");
            return;
        }

        // Generate sales order number
        var lastOrder = _dbContext.SalesOrders.OrderByDescending(so => so.Id).FirstOrDefault();
        int nextNum = (lastOrder?.Id ?? 0) + 1;
        string orderNumber = $"SO-{nextNum:D5}";

        var salesOrder = new SalesOrder
        {
            SalesOrderNumber = orderNumber,
            CreatedAt = DateTime.Now,
            TotalAmount = _cartItems.Sum(c => c.Total),
            Status = 0, // Pending
            TerminalName = "Salesman",
            CustomerName = string.IsNullOrWhiteSpace(CustomerNameEntry.Text)
                ? null : CustomerNameEntry.Text.Trim(),
            Items = _cartItems.Select(c => new SalesOrderItem
            {
                MenuItemId = c.MenuItemId,
                Quantity = c.Quantity,
                PricePerItem = c.Price,
                TotalPrice = c.Total
            }).ToList()
        };

        _dbContext.SalesOrders.Add(salesOrder);
        await _dbContext.SaveChangesAsync();

        // Print sales order slip
        string slipText = BuildOrderSlip(salesOrder);
        await PrintToThermal(slipText);

        await DisplayAlert("Order Created",
            $"{orderNumber} created — {_cartItems.Count} items, LKR {salesOrder.TotalAmount:N2}\n\nGive the printed slip to the customer for payment at the cashier.",
            "OK");

        // Clear cart for next order
        _cartItems.Clear();
        CustomerNameEntry.Text = string.Empty;
        LoadItems();
        UpdateTotal();
        RefreshCart();
    }

    private string BuildOrderSlip(SalesOrder order)
    {
        const int W = 42;
        var sb = new StringBuilder();

        string Line(char c = '-') => new string(c, W);
        string Center(string s) => s.PadLeft((W + s.Length) / 2).PadRight(W);
        string Row(string left, string right) => left + right.PadLeft(W - left.Length);

        sb.AppendLine(Center("The Royal Bakery"));
        sb.AppendLine(Center("202, Galle Road, Colombo-06"));
        sb.AppendLine(Line('='));
        sb.AppendLine(Center("*** SALES ORDER ***"));
        sb.AppendLine(Line());
        sb.AppendLine(Row("Order #:", order.SalesOrderNumber));
        sb.AppendLine(Row("Date:", order.CreatedAt.ToString("dd/MM/yyyy HH:mm")));
        if (!string.IsNullOrEmpty(order.CustomerName))
            sb.AppendLine(Row("Customer:", order.CustomerName));
        sb.AppendLine(Line());

        foreach (var item in order.Items)
        {
            var menuItem = _dbContext.MenuItems.Find(item.MenuItemId);
            string itemName = menuItem?.Name ?? "Unknown";
            sb.AppendLine(itemName);
            sb.AppendLine(Row($" {item.Quantity} x {item.PricePerItem:N2}", $"{item.TotalPrice:N2}"));
        }

        sb.AppendLine(Line());
        sb.AppendLine(Row("TOTAL", $"LKR {order.TotalAmount:N2}"));
        sb.AppendLine(Line('='));
        sb.AppendLine(Center($"[ {order.SalesOrderNumber} ]"));
        sb.AppendLine(Center("Present at cashier for payment"));
        sb.AppendLine(Line());
        sb.AppendLine(Center("Powered by EzyCode"));
        sb.AppendLine(Center("www.ezycode.lk"));

        return sb.ToString();
    }

    private async Task PrintToThermal(string text)
    {
        // Save locally as backup
        try
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "orders");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"order_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(filePath, text);
        }
        catch { }

        try
        {
            string printerPath = Preferences.Get("ThermalPrinterPath", "");
            if (!string.IsNullOrEmpty(printerPath))
            {
                byte[] init = { 0x1B, 0x40 };
                byte[] alignLeft = { 0x1B, 0x61, 0x00 };
                byte[] feedCut = { 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x41, 0x03 };
                byte[] textBytes = Encoding.GetEncoding("IBM437").GetBytes(text);

                using var fs = new FileStream(printerPath, FileMode.Open, FileAccess.Write);
                await fs.WriteAsync(init);
                await fs.WriteAsync(alignLeft);
                await fs.WriteAsync(textBytes);
                await fs.WriteAsync(feedCut);
                await fs.FlushAsync();
            }
        }
        catch { }
    }

    private async void OrderHistory_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new NavigationPage(new OrderHistoryPage())
        {
            BarBackgroundColor = Color.FromArgb("#1A1A1A"),
            BarTextColor = Colors.White
        });
    }

    private void UpdateTotal() => TotalLabel.Text = $"Total: LKR{_cartItems.Sum(c => c.Total):F2}";

    private void RefreshCart()
    {
        CartCollectionView.ItemsSource = null;
        CartCollectionView.ItemsSource = _cartItems;
    }

    private void RefreshItemsList()
    {
        ItemsCollectionView.ItemsSource = null;
        ItemsCollectionView.ItemsSource = _filteredItems;
    }

    // ===== View Models =====
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

    // ===== ORDER HISTORY PAGE =====
    public class OrderHistoryPage : ContentPage
    {
        public OrderHistoryPage()
        {
            Title = "Order History";
            BackgroundColor = Color.FromArgb("#1A1A1A");

            var db = new StockDbContext();
            var orders = db.SalesOrders
                .Include(so => so.Items)
                .OrderByDescending(so => so.CreatedAt)
                .Take(50)
                .ToList();

            var listView = new CollectionView
            {
                ItemsSource = orders,
                ItemTemplate = new DataTemplate(() =>
                {
                    var frame = new Frame
                    {
                        Padding = 12,
                        Margin = new Thickness(0, 4),
                        BackgroundColor = Color.FromArgb("#2A2A2A"),
                        CornerRadius = 10,
                        BorderColor = Color.FromArgb("#404040")
                    };

                    var grid = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                        RowSpacing = 2
                    };

                    var orderNum = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
                    orderNum.SetBinding(Label.TextProperty, "SalesOrderNumber");

                    var date = new Label { FontSize = 12, TextColor = Color.FromArgb("#AAAAAA") };
                    date.SetBinding(Label.TextProperty, new Binding("CreatedAt", stringFormat: "{0:dd MMM yyyy, hh:mm tt}"));

                    var customer = new Label { FontSize = 12, TextColor = Color.FromArgb("#888888") };
                    customer.SetBinding(Label.TextProperty, new Binding("CustomerName", stringFormat: "Customer: {0}"));

                    var total = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FFEB3B"), HorizontalTextAlignment = TextAlignment.End };
                    total.SetBinding(Label.TextProperty, new Binding("TotalAmount", stringFormat: "LKR {0:N2}"));

                    var statusLabel = new Label { FontSize = 11, HorizontalTextAlignment = TextAlignment.End };
                    statusLabel.SetBinding(Label.TextProperty, new Binding("Status", converter: new StatusConverter()));
                    statusLabel.SetBinding(Label.TextColorProperty, new Binding("Status", converter: new StatusColorConverter()));

                    grid.Add(orderNum, 0, 0);
                    grid.Add(date, 0, 1);
                    grid.Add(customer, 0, 2);
                    grid.Add(total, 1, 0);
                    grid.Add(statusLabel, 1, 1);

                    frame.Content = grid;
                    return frame;
                })
            };

            var closeBtn = new Button
            {
                Text = "Close",
                BackgroundColor = Color.FromArgb("#757575"),
                TextColor = Colors.White,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 48,
                CornerRadius = 10
            };
            closeBtn.Clicked += async (s, e) => await Navigation.PopModalAsync();

            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
                Padding = 20,
                RowSpacing = 12,
                Children =
                {
                    new Label { Text = "Sales Order History", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                }
            };

            var mainGrid = (Grid)Content;
            Grid.SetRow(listView, 1);
            Grid.SetRow(closeBtn, 2);
            mainGrid.Children.Add(listView);
            mainGrid.Children.Add(closeBtn);
        }

        private class StatusConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return (int)value switch { 0 => "Pending", 1 => "Paid", 2 => "Cancelled", _ => "Unknown" };
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => 0;
        }

        private class StatusColorConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return (int)value switch
                {
                    0 => Color.FromArgb("#FF9800"), // Pending - orange
                    1 => Color.FromArgb("#4CAF50"), // Paid - green
                    2 => Color.FromArgb("#F44336"), // Cancelled - red
                    _ => Colors.White
                };
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Colors.White;
        }
    }
}
