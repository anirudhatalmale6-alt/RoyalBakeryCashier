using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RoyalBakeryCashier.Data;
using Microsoft.EntityFrameworkCore;

namespace RoyalBakeryCashier.Pages
{
    public partial class PaymentPopupPage : ContentPage
    {
        private readonly decimal _total;
        private readonly Action<RoyalBakeryCashier.ViewModels.OrderDetailsViewModel.PaymentResult> _onComplete;

        public PaymentPopupPage(
            decimal total,
            Action<RoyalBakeryCashier.ViewModels.OrderDetailsViewModel.PaymentResult> onComplete,
            System.Collections.Generic.IEnumerable<RoyalBakeryCashier.ViewModels.OrderDetailsViewModel.OrderItemDisplay> itemsToCheck)
        {
            InitializeComponent();
            _total = total;
            _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));

            // default tender to total
            TenderEntry.Text = total.ToString("F2");
            TenderEntry.TextChanged += (s, e) => UpdateBalance();

            // Check stocks for each item and show shortages if any
            CheckStockAvailability(itemsToCheck);

            UpdateBalance();
        }

        void CheckStockAvailability(System.Collections.Generic.IEnumerable<RoyalBakeryCashier.ViewModels.OrderDetailsViewModel.OrderItemDisplay> items)
        {
            if (items == null) return;

            using var db = new StockDbContext();
            StockProblemsList.Children.Clear();

            var shortages = items
                .Select(item =>
                {
                    var stock = db.Stocks.FirstOrDefault(s => s.MenuItemId == item.MenuItemId);
                    var available = stock?.Quantity ?? 0;
                    return new { Item = item, Available = available, Short = item.Quantity - available };
                })
                .Where(x => x.Short > 0)
                .ToList();

            if (!shortages.Any())
            {
                StockProblemsFrame.IsVisible = false;
                ConfirmButton.IsEnabled = true;
                return;
            }

            StockProblemsFrame.IsVisible = true;
            ConfirmButton.IsEnabled = false;

            foreach (var s in shortages)
            {
                var txt = $"{s.Item.Name} — requested: {s.Item.Quantity}, available: {s.Available}";
                StockProblemsList.Children.Add(new Label { Text = txt, TextColor = Colors.DarkRed, FontSize = 14 });
            }
        }

        void UpdateBalance()
        {
            if (decimal.TryParse(TenderEntry.Text, out var tender))
            {
                var change = tender - _total;
                if (change >= 0)
                {
                    BalanceLabel.Text = $"Change: LKR {change:N2}";
                    BalanceLabel.TextColor = Colors.Green;
                }
                else
                {
                    BalanceLabel.Text = $"Outstanding: LKR {Math.Abs(change):N2}";
                    BalanceLabel.TextColor = Colors.OrangeRed;
                }
            }
            else
            {
                BalanceLabel.Text = "Enter tender";
                BalanceLabel.TextColor = Colors.Gray;
            }
        }

        async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        async void OnConfirmClicked(object sender, EventArgs e)
        {
            // If stock problems are visible, block confirming (safety)
            if (StockProblemsFrame.IsVisible)
            {
                await DisplayAlert("Stock problem", "One or more items are not available in the requested quantity. Adjust the order before paying.", "OK");
                return;
            }

            if (!decimal.TryParse(TenderEntry.Text, out var tender))
            {
                // invalid input; show a simple alert
                await DisplayAlert("Invalid", "Please enter a valid tender amount.", "OK");
                return;
            }

            var method = CashRadio.IsChecked ? "Cash" : "Card";
            var result = new RoyalBakeryCashier.ViewModels.OrderDetailsViewModel.PaymentResult
            {
                Method = method,
                Tender = tender
            };

            // notify caller
            _onComplete(result);

            // close modal
            await Navigation.PopModalAsync();
        }
    }
}