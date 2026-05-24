using Plugin.FirebaseAuth;
using UniMart.Services;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace UniMart.Views
{
    public partial class CheckoutPage : ContentPage
    {
        private double _topUpAmount;
        private StripePaymentService _stripeService;
        private TransactionEngine _engine;

        private bool _isFormattingCard;
        private bool _isFormattingExpiry;
        private bool _isProcessingPayment = false;

        public CheckoutPage(double amount)
        {
            InitializeComponent();
            _topUpAmount = amount;
            _stripeService = new StripePaymentService();
            _engine = new TransactionEngine();

            AmountLabel.Text = $"{_topUpAmount:F2}";
        }

        protected override bool OnBackButtonPressed()
        {
            if (_isProcessingPayment) return true;
            return base.OnBackButtonPressed();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (_isProcessingPayment) return;
            await Navigation.PopModalAsync();
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

        private async void OnInputFocused(object sender, FocusEventArgs e)
        {
            if (e.IsFocused && sender is Element focusedElement)
            {
                if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 260;
                await Task.Delay(300);
                if (FormScrollView != null) await FormScrollView.ScrollToAsync(focusedElement, ScrollToPosition.Center, true);
            }
        }

        private async void OnInputUnfocused(object sender, FocusEventArgs e)
        {
            await Task.Delay(100);
            if (!CardNumberEntry.IsFocused && !ExpiryEntry.IsFocused && !CvcEntry.IsFocused)
            {
                if (FormScrollView != null) await FormScrollView.ScrollToAsync(0, 0, true);
                if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 0;
            }
        }

        private void OnCardNumberChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingCard) return;

            string unformatted = e.NewTextValue?.Replace(" ", "") ?? "";
            string formatted = "";

            for (int i = 0; i < unformatted.Length; i++)
            {
                if (i > 0 && i % 4 == 0) formatted += " ";
                formatted += unformatted[i];
            }

            if (CardNumberEntry.Text != formatted)
            {
                _isFormattingCard = true;
                Dispatcher.Dispatch(() =>
                {
                    CardNumberEntry.Text = formatted;
                    CardNumberEntry.CursorPosition = formatted.Length;
                    VisualCardNumberLabel.Text = string.IsNullOrEmpty(formatted) ? "4242 42XX XXXX 4242" : formatted;
                    _isFormattingCard = false;
                });
            }
            else
            {
                VisualCardNumberLabel.Text = string.IsNullOrEmpty(formatted) ? "4242 42XX XXXX 4242" : formatted;
            }
        }

        // 🔥 FIX: Custom string parser instantly handles MM/YY logic
        private void OnExpiryChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingExpiry) return;

            string unformatted = e.NewTextValue?.Replace("/", "") ?? "";
            string formatted = "";

            if (unformatted.Length > 0)
            {
                if (unformatted.Length > 2)
                    formatted = unformatted.Substring(0, 2) + "/" + unformatted.Substring(2);
                else
                    formatted = unformatted;
            }

            if (ExpiryEntry.Text != formatted)
            {
                _isFormattingExpiry = true;
                Dispatcher.Dispatch(() =>
                {
                    ExpiryEntry.Text = formatted;
                    ExpiryEntry.CursorPosition = formatted.Length;
                    VisualCardExpiryLabel.Text = string.IsNullOrEmpty(formatted) ? "MM/YY" : formatted;
                    _isFormattingExpiry = false;
                });
            }
            else
            {
                VisualCardExpiryLabel.Text = string.IsNullOrEmpty(formatted) ? "MM/YY" : formatted;
            }
        }

        private void OnCardCvcChanged(object sender, TextChangedEventArgs e)
        {
            VisualCardCvcLabel.Text = CvcEntry.Text ?? "XXX";
        }

        private async void OnPayClicked(object sender, EventArgs e)
        {
            CardNumberEntry.Unfocus();
            ExpiryEntry.Unfocus();
            CvcEntry.Unfocus();

            string card = CardNumberEntry.Text?.Trim().Replace(" ", "");
            string expiry = ExpiryEntry.Text?.Trim() ?? "";
            string cvc = CvcEntry.Text?.Trim();

            string month = "";
            string year = "";

            if (expiry.Contains("/"))
            {
                var parts = expiry.Split('/');
                if (parts.Length == 2)
                {
                    month = parts[0];
                    year = parts[1];
                }
            }

            if (string.IsNullOrWhiteSpace(card) || string.IsNullOrWhiteSpace(month) || string.IsNullOrWhiteSpace(year) || string.IsNullOrWhiteSpace(cvc))
            {
                await ShowCustomToast("Missing Info", "Please fill in all card details.", true);
                return;
            }

            _isProcessingPayment = true;
            LoadingOverlay.IsVisible = true;

            // 🔥 FIX: Removed emojis for premium feel
            LoadingStatusLabel.Text = "Verifying Securely...";

            var result = await _stripeService.ProcessPaymentAsync(_topUpAmount, card, month, year, cvc);

            if (result.IsSuccess)
            {
                LoadingStatusLabel.Text = "Writing to Ledger...";

                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser != null)
                {
                    bool success = await _engine.TopUpBalanceAsync(currentUser.Uid, _topUpAmount, result.ResultData);

                    if (success)
                    {
                        LoadingStatusLabel.Text = "Payment Complete!";

                        // 🔥 FIX: Dropped the delay to 200ms so it snaps back to Wallet instantly without lag
                        await Task.Delay(200);
                        _isProcessingPayment = false;
                        await Navigation.PopModalAsync();
                        return;
                    }
                    else
                    {
                        await ShowCustomToast("Ledger Error", "Transaction failed. Funds not captured.", true);
                    }
                }
            }
            else
            {
                await ShowCustomToast("Payment Failed", result.ResultData, true);
            }

            LoadingOverlay.IsVisible = false;
            _isProcessingPayment = false;
        }
    }
}