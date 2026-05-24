using UniMart.Views;
using Microsoft.Maui.Controls;

namespace UniMart
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes
            Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));
            Routing.RegisterRoute(nameof(BuyerHandoverPage), typeof(BuyerHandoverPage));
            Routing.RegisterRoute(nameof(SellerScannerPage), typeof(SellerScannerPage));
            Routing.RegisterRoute(nameof(ItemDetailsPage), typeof(ItemDetailsPage));
            Routing.RegisterRoute(nameof(SellerDashboardPage), typeof(SellerDashboardPage));
            Routing.RegisterRoute(nameof(BuyerOrdersPage), typeof(BuyerOrdersPage));
            Routing.RegisterRoute(nameof(MeetupChatPage), typeof(MeetupChatPage));
            Routing.RegisterRoute(nameof(NoInternetPage), typeof(NoInternetPage));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(RegistrationPage), typeof(RegistrationPage));

            // 🔥 GLOBAL LAYOUT FIX: Listen for any navigation change
            this.Navigated += OnNavigated;
        }

        private void OnNavigated(object sender, ShellNavigatedEventArgs e)
        {
            // When navigation finishes, force the current page to recalculate its layout.
            // This pulls the content back to the top and removes any "ghost space"
            // left by changing TabBar visibility.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (CurrentPage != null)
                {
                    CurrentPage.ForceLayout();
                }
            });
        }
    }
}