using UniMart.Config;
using ZXing.Net.Maui.Controls;
using UniMart.Views;
using UniMart.Services;

namespace UniMart
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // 1. CORE BUILDER SETUP (Clean, single chain)
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 2. DEPENDENCY INJECTION: Core Services
            // We use AddSingleton so the entire app shares the EXACT SAME instance of these engines.
            builder.Services.AddSingleton<StripePaymentService>();
            builder.Services.AddSingleton<TransactionEngine>();

            // 3. DEPENDENCY INJECTION: Views (Pages)
            // 🔥 THE FIX: Removed the Shell Tabs (Home, Sell, Wallet, Profile, Inbox) 
            // from the DI container. AppShell must manage their memory lifecycle natively to prevent wake-up crashes.

            // Hidden Pages (Pages that are pushed onto the navigation stack MUST stay Transient)
            builder.Services.AddTransient<CheckoutPage>();
            builder.Services.AddTransient<ItemDetailsPage>();
            builder.Services.AddTransient<MeetupChatPage>();
            builder.Services.AddTransient<BuyerHandoverPage>();
            builder.Services.AddTransient<SellerScannerPage>();
            builder.Services.AddTransient<SellerDashboardPage>();
            builder.Services.AddTransient<BuyerOrdersPage>();
            builder.Services.AddTransient<NoInternetPage>();
            builder.Services.AddTransient<LoginPage>();

            return builder.Build();
        }
    }
}