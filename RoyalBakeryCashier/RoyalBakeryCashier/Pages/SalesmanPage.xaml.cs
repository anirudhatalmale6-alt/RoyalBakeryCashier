using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using RoyalBakeryCashier.Helpers;
using System.Collections.ObjectModel;
using System.Text;

namespace RoyalBakeryCashier.Pages;

public partial class SalesmanPage : ContentPage
{
    private readonly StockDbContext _dbContext;
    private ObservableCollection<ItemViewModel> _allItems;
    private ObservableCollection<ItemViewModel> _filteredItems;
    private ObservableCollection<CartItem> _cartItems;

    private bool _loaded = false;

    public SalesmanPage()
    {
        InitializeComponent();
        _dbContext = new StockDbContext();
        _cartItems = new ObservableCollection<CartItem>();
        CartCollectionView.ItemsSource = _cartItems;
        Title = $"{App.TerminalName} Terminal";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        try
        {
            await Task.Run(() =>
            {
                _dbContext.Database.EnsureCreated();
                _dbContext.ApplyMigrations();
            });
            LoadCategories();
            LoadItems();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Database Error",
                $"Could not connect to SQL Server.\n\nMake sure SQL Server is running and the database exists.\n\nError: {ex.Message}",
                "OK");
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

        int cols = 6;
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
                CornerRadius = 0,
                FontSize = 14,
                HeightRequest = 50,
                Margin = 0,
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
            TerminalName = App.TerminalName,
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

        // Print sales order slip with QR code
        await PrintOrderSlip(salesOrder);

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

    private async Task PrintOrderSlip(SalesOrder order)
    {
        const int W = 48;
        string Separator(char c = '-') => new string(c, W);
        string Row(string l, string r) => l + r.PadLeft(W - l.Length);

        // Save locally as backup
        try
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "orders");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(
                Path.Combine(dir, $"order_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),
                $"Order: {order.SalesOrderNumber} | Total: LKR {order.TotalAmount:N2}");
        }
        catch { }

        try
        {
            // Find printer: saved preference or auto-detect
            string printerName = Preferences.Get("ThermalPrinterName", "");
            if (string.IsNullOrEmpty(printerName))
            {
                printerName = RawPrinterHelper.FindThermalPrinter() ?? "";
                if (!string.IsNullOrEmpty(printerName))
                    Preferences.Set("ThermalPrinterName", printerName);
            }

            if (string.IsNullOrEmpty(printerName))
            {
                var printers = RawPrinterHelper.GetInstalledPrinters();
                string msg = printers.Count > 0
                    ? $"No thermal printer detected.\n\nInstalled printers:\n{string.Join("\n", printers)}"
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
            void Emit(byte[] b) => ms.Write(b, 0, b.Length);

            Emit(init);

            // Header — centered
            Emit(center);
            Emit(enc.GetBytes("The Royal Bakery\n"));
            Emit(enc.GetBytes("202, Galle Road, Colombo-06\n"));
            Emit(enc.GetBytes("0112 500 991 / 0114 341 642\n"));
            Emit(enc.GetBytes("www.theroyalbakery.com\n"));
            Emit(enc.GetBytes(Separator('=') + "\n"));
            Emit(enc.GetBytes("*** SALES ORDER ***\n"));
            Emit(enc.GetBytes(Separator() + "\n"));

            // Body — left aligned
            Emit(left);
            Emit(enc.GetBytes(Row("Order #:", order.SalesOrderNumber) + "\n"));
            Emit(enc.GetBytes(Row("Date:", order.CreatedAt.ToString("dd/MM/yyyy HH:mm")) + "\n"));
            Emit(enc.GetBytes(Row("Terminal:", App.TerminalName) + "\n"));
            if (!string.IsNullOrEmpty(order.CustomerName))
                Emit(enc.GetBytes(Row("Customer:", order.CustomerName) + "\n"));
            Emit(enc.GetBytes(Separator() + "\n"));

            foreach (var item in order.Items)
            {
                var menuItem = _dbContext.MenuItems.Find(item.MenuItemId);
                string itemName = menuItem?.Name ?? "Unknown";
                Emit(enc.GetBytes(itemName + "\n"));
                Emit(enc.GetBytes(Row($" {item.Quantity} x {item.PricePerItem:N2}", $"{item.TotalPrice:N2}") + "\n"));
            }

            Emit(enc.GetBytes(Separator() + "\n"));
            Emit(enc.GetBytes(Row("TOTAL", $"Rs. {order.TotalAmount:N2}") + "\n"));
            Emit(enc.GetBytes(Separator('=') + "\n"));

            // QR code + footer — centered
            Emit(center);
            byte[] qrBytes = BuildEscPosQR(order.SalesOrderNumber);
            Emit(qrBytes);
            Emit(enc.GetBytes("\n"));
            Emit(enc.GetBytes($"[ {order.SalesOrderNumber} ]\n"));
            Emit(enc.GetBytes("Present at cashier for payment\n"));
            Emit(enc.GetBytes(Separator() + "\n"));
            Emit(enc.GetBytes("Powered by EzyCode\n"));
            Emit(enc.GetBytes("www.ezycode.lk\n"));

            Emit(feedCut);

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

    /// <summary>
    /// Build native ESC/POS QR code command bytes for Epson thermal printers.
    /// Uses GS ( k commands supported by TM-T82, TM-T88, etc.
    /// </summary>
    private static byte[] BuildEscPosQR(string data)
    {
        byte[] dataBytes = Encoding.ASCII.GetBytes(data);
        int storeLen = dataBytes.Length + 3; // pL pH 31 50 30 + data
        byte pL = (byte)(storeLen & 0xFF);
        byte pH = (byte)((storeLen >> 8) & 0xFF);

        using var ms = new MemoryStream();

        // 1. Select model (Model 2)
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });

        // 2. Set module size (6 = medium, good for scanning)
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06 });

        // 3. Set error correction level (L = 48, M = 49, Q = 50, H = 51)
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31 });

        // 4. Store QR data
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, pL, pH, 0x31, 0x50, 0x30 });
        ms.Write(dataBytes);

        // 5. Print QR code
        ms.Write(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

        return ms.ToArray();
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
