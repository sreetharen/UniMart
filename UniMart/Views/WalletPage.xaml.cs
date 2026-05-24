using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using UniMart.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace UniMart.Views
{
    public partial class WalletPage : ContentPage
    {
        private Plugin.CloudFirestore.IListenerRegistration _walletListener;
        private Plugin.CloudFirestore.IListenerRegistration _transactionsListener;

        public WalletPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartRealTimeLedger();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _walletListener?.Remove();
            _transactionsListener?.Remove();
        }

        // 🔥 FIX: Programmatically eject focus to close keyboard on 'Done'
        private void OnAmountCompleted(object sender, EventArgs e)
        {
            Shell.SetTabBarIsVisible(this, true);
            AmountEntry.Unfocus();
        }

        // 🔥 FIX: Dynamic Scrolling Engine for Wallet
        private async void OnAmountFocused(object sender, FocusEventArgs e)
        {
            Shell.SetTabBarIsVisible(this, false);

            if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 260;

            await Task.Delay(300);

            if (WalletScrollView != null)
                await WalletScrollView.ScrollToAsync(AmountEntry, ScrollToPosition.Center, true);
        }

        private async void OnAmountUnfocused(object sender, FocusEventArgs e)
        {
            // Reduced delay so the layout snaps back faster
            await Task.Delay(50);

            if (!AmountEntry.IsFocused)
            {
                // Failsafe: Bring it back just in case they tapped the background instead of "Done"
                Shell.SetTabBarIsVisible(this, true);

                if (WalletScrollView != null)
                    await WalletScrollView.ScrollToAsync(0, 0, true);

                if (KeyboardSpacer != null)
                    KeyboardSpacer.HeightRequest = 0;
            }
        }

        private async Task ShowCustomToast(string title, string message, bool isError = false)
        {
            if (CustomAlertBox == null) return;
            AlertTitle.Text = title;
            AlertMessage.Text = message;
            AlertBorder.BackgroundColor = isError ? Color.FromArgb("#DC3545") : Color.FromArgb("#198754");
            AlertIcon.Text = isError ? "✕" : "✓";

            await CustomAlertBox.TranslateTo(0, 40, 400, Easing.CubicOut);
            await Task.Delay(3000);
            await CustomAlertBox.TranslateTo(0, -150, 300, Easing.CubicIn);
        }

        private void StartRealTimeLedger()
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null) return;

            var db = CrossCloudFirestore.Current.Instance;

            _walletListener = db.Collection("users").Document(currentUser.Uid)
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (snapshot != null && snapshot.Exists)
                    {
                        var userProfile = snapshot.ToObject<User>();
                        if (userProfile != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                BalanceLabel.Text = $"{userProfile.Wallet.AvailableBalance:F2}";
                                EscrowLabel.Text = $"Locked in Escrow: RM {userProfile.Wallet.PendingEscrow:F2}";
                            });
                        }
                    }
                });

            _transactionsListener = db.Collection("transactions")
                .WhereEqualsTo("userId", currentUser.Uid)
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (snapshot != null)
                    {
                        var transactions = snapshot.Documents
                            .Select(d => d.ToObject<TransactionRecord>())
                            .OrderByDescending(t => t.Date)
                            .ToList();

                        MainThread.BeginInvokeOnMainThread(() => TransactionsList.ItemsSource = transactions);
                    }
                });
        }

        private void OnQuickAmountClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string amt)
            {
                double.TryParse(AmountEntry.Text, out double current);
                AmountEntry.Text = (current + double.Parse(amt)).ToString("F2");
            }
        }

        private async void OnCheckoutClicked(object sender, EventArgs e)
        {
            AmountEntry.Unfocus();

            if (!double.TryParse(AmountEntry.Text, out double amount) || amount < 5)
            {
                await ShowCustomToast("Invalid Amount", "Minimum top-up is RM 5.00", true);
                return;
            }

            // 🔥 FIX: Pushing the page directly without the NavigationPage wrapper kills the blue status bar bug
            await Navigation.PushModalAsync(new CheckoutPage(amount));
            AmountEntry.Text = string.Empty;
        }

        private async void OnWithdrawClicked(object sender, EventArgs e) =>
            await Navigation.PushModalAsync(new WithdrawPage());
    }
}