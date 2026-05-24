using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
using System;

namespace UniMart.Views
{
    public partial class NoInternetPage : ContentPage
    {
        // 🔥 NEW: The Safety Lock to prevent double-popping crashes
        private bool _isDismissing = false;

        public NoInternetPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _isDismissing = false; // Reset the lock when the page opens

            // 1. Double check: If the internet is already back before this finishes loading, just leave immediately.
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                DismissPage();
                return;
            }

            // 2. Start listening to the device's network hardware
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // 3. Prevent memory leaks by unsubscribing when the page closes
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        }

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            // 🔥 THE EXPERT MAGIC: The exact millisecond the signal returns, auto-close this screen!
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DismissPage();
                });
            }
        }

        // 🔥 THE EXPERT FIX: Bulletproof Navigation
        private async void DismissPage()
        {
            // 1. If we are already closing, ignore any extra Wi-Fi events
            if (_isDismissing) return;
            _isDismissing = true;

            try
            {
                // 2. Safely check how the page was opened before closing it
                if (Navigation.ModalStack.Count > 0)
                {
                    await Navigation.PopModalAsync();
                }
                else if (Navigation.NavigationStack.Count > 1)
                {
                    // Fallback just in case you used PushAsync instead of PushModalAsync
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                // Silently handle any edge-case navigation errors
                Console.WriteLine($"Navigation Guard: {ex.Message}");
            }
        }

        // The Hardware Backdoor Lock (Prevents them from bypassing this screen)
        protected override bool OnBackButtonPressed()
        {
            return true;
        }
    }
}