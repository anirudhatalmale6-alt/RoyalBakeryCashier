using RoyalBakeryCashier.ViewModels;
using RoyalBakeryCashier.Data.Entities;

namespace RoyalBakeryCashier.Pages;

public partial class EnterOrderPage : ContentPage
{
    private readonly EnterOrderViewModel _vm;
    public EnterOrderPage()
	{
		InitializeComponent();

        // Create viewmodel and set BindingContext
        _vm = new EnterOrderViewModel();

        // When the VM finds an order, navigate to details page and pass the Order entity.
        _vm.OnOrderReady = async order =>
        {
            if (order == null) return;
            await Navigation.PushAsync(new OrderDetailsPage(order));
        };

        BindingContext = _vm;
    }
}