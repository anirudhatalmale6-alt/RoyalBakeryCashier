using Microsoft.EntityFrameworkCore;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using System.Collections.ObjectModel;

namespace RoyalBakeryCashier.Pages;

public partial class GRNPage : ContentPage
{
    private readonly StockDbContext _db;
    private List<MenuItem> _menuItems;
    private ObservableCollection<GRNItemRow> _grnItems;

    public GRNPage()
    {
        InitializeComponent();
        _db = new StockDbContext();
        _grnItems = new ObservableCollection<GRNItemRow>();
        GRNItemsView.ItemsSource = _grnItems;

        LoadMenuItems();
        LoadGRNList();
    }

    private void LoadMenuItems()
    {
        _menuItems = _db.MenuItems.OrderBy(m => m.Name).ToList();
        MenuItemPicker.ItemsSource = _menuItems.Select(m => m.Name).ToList();
    }

    private void LoadGRNList()
    {
        var grns = _db.GRNs
            .Include(g => g.Items)
            .OrderByDescending(g => g.CreatedAt)
            .Take(50)
            .Select(g => new GRNViewModel
            {
                GRNId = g.Id,
                GRNNumber = g.GRNNumber,
                CreatedAt = g.CreatedAt,
                ItemCount = g.Items.Count
            })
            .ToList();

        GRNListView.ItemsSource = grns;
    }

    private void AddGRNItem_Clicked(object sender, EventArgs e)
    {
        if (MenuItemPicker.SelectedIndex < 0)
        {
            DisplayAlert("Error", "Please select an item.", "OK");
            return;
        }

        if (!int.TryParse(GRNQtyEntry.Text, out int qty) || qty <= 0)
        {
            DisplayAlert("Error", "Please enter a valid quantity.", "OK");
            return;
        }

        if (!decimal.TryParse(GRNCostEntry.Text, out decimal cost) || cost < 0)
        {
            DisplayAlert("Error", "Please enter a valid cost price.", "OK");
            return;
        }

        var menuItem = _menuItems[MenuItemPicker.SelectedIndex];

        // Check if already added
        var existing = _grnItems.FirstOrDefault(i => i.MenuItemId == menuItem.Id);
        if (existing != null)
        {
            existing.Quantity += qty;
            existing.CostPrice = cost; // update to latest cost
            RefreshGRNItems();
        }
        else
        {
            _grnItems.Add(new GRNItemRow
            {
                MenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                Quantity = qty,
                CostPrice = cost
            });
        }

        // Clear inputs
        GRNQtyEntry.Text = string.Empty;
        GRNCostEntry.Text = string.Empty;
        MenuItemPicker.SelectedIndex = -1;
    }

    private void RemoveGRNItem_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is GRNItemRow item)
            _grnItems.Remove(item);
    }

    private async void SaveGRN_Clicked(object sender, EventArgs e)
    {
        if (_grnItems.Count == 0)
        {
            await DisplayAlert("Error", "Add at least one item to the GRN.", "OK");
            return;
        }

        // Generate GRN number
        var lastGrn = _db.GRNs.OrderByDescending(g => g.Id).FirstOrDefault();
        int nextNum = (lastGrn?.Id ?? 0) + 1;
        string grnNumber = $"GRN-{nextNum:D5}";

        var grn = new GRN
        {
            GRNNumber = grnNumber,
            CreatedAt = DateTime.Now,
            Items = _grnItems.Select(i => new GRNItem
            {
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity,
                Price = i.CostPrice,
                CurrentQuantity = i.Quantity // initially all available
            }).ToList()
        };

        _db.GRNs.Add(grn);

        // Also update stock quantities
        foreach (var item in _grnItems)
        {
            var stock = _db.Stocks.FirstOrDefault(s => s.MenuItemId == item.MenuItemId);
            if (stock != null)
                stock.Quantity += item.Quantity;
            else
                _db.Stocks.Add(new Stock { MenuItemId = item.MenuItemId, Quantity = item.Quantity });
        }

        await _db.SaveChangesAsync();

        await DisplayAlert("Saved", $"{grnNumber} saved with {_grnItems.Count} items. Stock updated.", "OK");

        _grnItems.Clear();
        LoadGRNList();
    }

    private async void NewGRN_Clicked(object sender, EventArgs e)
    {
        FormTitle.Text = "New GRN";
        _grnItems.Clear();
        SaveGRNBtn.IsVisible = true;
        MenuItemPicker.IsEnabled = true;
        GRNQtyEntry.IsEnabled = true;
        GRNCostEntry.IsEnabled = true;
    }

    private async void ViewGRN_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is int grnId)
        {
            var grn = _db.GRNs
                .Include(g => g.Items)
                .ThenInclude(i => i.MenuItem)
                .FirstOrDefault(g => g.Id == grnId);

            if (grn == null) return;

            FormTitle.Text = $"{grn.GRNNumber} — {grn.CreatedAt:dd MMM yyyy}";

            _grnItems.Clear();
            foreach (var item in grn.Items)
            {
                _grnItems.Add(new GRNItemRow
                {
                    MenuItemId = item.MenuItemId,
                    ItemName = item.MenuItem?.Name ?? "Unknown",
                    Quantity = item.Quantity,
                    CostPrice = item.Price
                });
            }

            // View-only mode
            SaveGRNBtn.IsVisible = false;
            MenuItemPicker.IsEnabled = false;
            GRNQtyEntry.IsEnabled = false;
            GRNCostEntry.IsEnabled = false;
        }
    }

    private async void Close_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private void RefreshGRNItems()
    {
        GRNItemsView.ItemsSource = null;
        GRNItemsView.ItemsSource = _grnItems;
    }

    // View models
    public class GRNItemRow
    {
        public int MenuItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public string QuantityText => $"x{Quantity}";
        public string CostText => $"LKR {CostPrice:N2}";
    }

    public class GRNViewModel
    {
        public int GRNId { get; set; }
        public string GRNNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public string DateFormatted => CreatedAt.ToString("dd MMM yyyy, hh:mm tt");
        public string ItemCountText => $"{ItemCount} items";
    }
}
