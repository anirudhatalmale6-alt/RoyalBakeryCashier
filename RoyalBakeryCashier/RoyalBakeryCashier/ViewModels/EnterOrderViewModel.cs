using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using RoyalBakeryCashier.Models;
using System.Collections.Generic;
using System.Linq;
using RoyalBakeryCashier.Data;
using RoyalBakeryCashier.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace RoyalBakeryCashier.ViewModels
{
    public class EnterOrderViewModel : INotifyPropertyChanged
    {
        private string _orderId;
        private string _statusMessage;

        public string OrderId
        {
            get => _orderId;
            set { _orderId = value; Raise(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; Raise(); }
        }

        public ICommand SimulateScanCommand { get; }
        public ICommand SubmitCommand { get; }

        // Callback that the page sets to receive the fetched Order and navigate.
        public Action<Order> OnOrderReady { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public EnterOrderViewModel()
        {
            SimulateScanCommand = new Command(OnSimulateScan);
            SubmitCommand = new Command(async () => await OnSubmitAsync());
        }

        private void OnSimulateScan()
        {
            // Simulate a QR scan result
            OrderId = "12345";
            StatusMessage = "Scanned: " + OrderId;
        }

        private async Task OnSubmitAsync()
        {
            if (string.IsNullOrWhiteSpace(OrderId))
            {
                StatusMessage = "Please enter or scan an Order ID.";
                return;
            }

            StatusMessage = "Fetching order...";
            try
            {
                // Try to parse numeric Order Id (Orders.Id is an int)
                if (!int.TryParse(OrderId.Trim(), out var id))
                {
                    StatusMessage = "Order ID must be a numeric Id.";
                    return;
                }

                using var db = new StockDbContext();
                // Load order and its items
                var order = await db.Orders
                                    .Include(o => o.Items)
                                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    StatusMessage = "Order not found in database.";
                    return;
                }

                StatusMessage = "Order found.";
                OnOrderReady?.Invoke(order);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error fetching order: " + ex.Message;
            }
        }

        protected void Raise([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}