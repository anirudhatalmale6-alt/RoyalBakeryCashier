using Microsoft.Extensions.Logging;

namespace RoyalBakeryCashier
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            #if DEBUG
    		            builder.Logging.AddDebug();
            #endif

            // Register pages so DI can resolve them at startup
            builder.Services.AddSingleton<Pages.EnterOrderPage>();
            builder.Services.AddTransient<Pages.OrderDetailsPage>();
            builder.Services.AddSingleton<Pages.CashierPage>();

            return builder.Build();
        }
    }
}
