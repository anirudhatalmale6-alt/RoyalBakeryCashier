using RoyalBakeryCashier.Data.Entities;
using RoyalBakeryCashier.ViewModels;
using Microsoft.Maui.Controls;

namespace RoyalBakeryCashier.Pages;

public partial class OrderDetailsPage : ContentPage
{
    public OrderDetailsPage(Order order)
    {
        InitializeComponent();
        var vm = new OrderDetailsViewModel(order);

        // subscribe to post-payment messages so UI can alert the cashier
        vm.PaymentCompleted = async (message) =>
        {
            // ensure we are on the main thread and show an alert
            await DisplayAlert("Payment", message, "OK");
        };

        BindingContext = vm;

        // wire the VM request to show the payment modal
        vm.OnRequestPayment = (applyPayment) =>
        {
            // show modal and let it call applyPayment when user confirms
            var popup = new PaymentPopupPage(vm.GrandTotal, result => applyPayment(result), vm.Items);
            _ = Navigation.PushModalAsync(popup);
        };
    }
}