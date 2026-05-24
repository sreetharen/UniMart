using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using UniMart.Models;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace UniMart.Views
{
    public partial class WithdrawPage : ContentPage
    {
        private double _currentAvailableBalance = 0;
        private bool _isProcessing = false;

        public WithdrawPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCurrentBalance();
        }

        protected override bool OnBackButtonPressed()
        {
            if (_isProcessing) return true; // Blocks physical back button during network calls
            return base.OnBackButtonPressed();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (_isProcessing) return;
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

            if (!AmountEntry.IsFocused && !BankPicker.IsFocused && !AccountNumberEntry.IsFocused && !AccountNameEntry.IsFocused)
            {
                if (FormScrollView != null) await FormScrollView.ScrollToAsync(0, 0, true);
                if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 0;
            }
        }

        private async Task LoadCurrentBalance()
        {
            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) return;

                var userDoc = await CrossCloudFirestore.Current.Instance
                  .Collection("users")
                  .Document(currentUser.Uid)
                  .GetAsync();

                var user = userDoc.ToObject<User>();
                if (user != null)
                {
                    _currentAvailableBalance = user.Wallet.AvailableBalance;
                    AvailableBalanceLabel.Text = $"RM {_currentAvailableBalance:F2}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching balance: {ex.Message}");
            }
        }

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            // Drop Keyboard safely before blocking UI
            AmountEntry.Unfocus();
            AccountNumberEntry.Unfocus();
            AccountNameEntry.Unfocus();

            string amountStr = AmountEntry.Text?.Trim();
            string bankName = BankPicker.SelectedItem?.ToString();
            string accNumber = AccountNumberEntry.Text?.Trim();
            string accName = AccountNameEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(amountStr) || string.IsNullOrWhiteSpace(bankName) ||
                string.IsNullOrWhiteSpace(accNumber) || string.IsNullOrWhiteSpace(accName))
            {
                await ShowCustomToast("Missing Information", "Please fill out all bank details.", true);
                return;
            }

            if (!double.TryParse(amountStr, out double withdrawAmount) || withdrawAmount <= 0)
            {
                await ShowCustomToast("Invalid Amount", "Please enter a valid withdrawal amount.", true);
                return;
            }

            if (withdrawAmount > _currentAvailableBalance)
            {
                await ShowCustomToast("Insufficient Funds", "You cannot withdraw more than your balance.", true);
                return;
            }

            _isProcessing = true;
            LoadingOverlay.IsVisible = true;

            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                var db = CrossCloudFirestore.Current.Instance;
                var userRef = db.Collection("users").Document(currentUser.Uid);

                string transactionId = Guid.NewGuid().ToString();
                var transactionRef = db.Collection("transactions").Document(transactionId);

                // Creates a queue document for the Admin Dashboard
                var adminQueueRef = db.Collection("withdrawal_requests").Document(transactionId);

                await db.RunTransactionAsync(transaction =>
                {
                    var userSnapshot = transaction.Get(userRef);
                    var user = userSnapshot.ToObject<User>();

                    if (user.Wallet.AvailableBalance < withdrawAmount)
                        throw new Exception("Insufficient funds at the time of processing.");

                    // Deduct the money
                    transaction.Update(userRef, "wallet.availableBalance", FieldValue.Increment(-withdrawAmount));

                    // Create the Audit Trail receipt
                    var receipt = new TransactionRecord
                    {
                        Id = transactionId,
                        UserId = currentUser.Uid,
                        Amount = withdrawAmount,
                        Type = "Withdrawal",
                        Description = $"Pending Transfer: {bankName}",
                        Date = DateTime.UtcNow
                    };
                    transaction.Set(transactionRef, receipt);

                    // 🔥 FIX: Send the request to the Admin Queue
                    var withdrawalRequest = new
                    {
                        Id = transactionId,
                        UserId = currentUser.Uid,
                        UserEmail = currentUser.Email,
                        Amount = withdrawAmount,
                        BankName = bankName,
                        AccountNumber = accNumber,
                        AccountName = accName,
                        Status = "Pending", // Pending, Approved, Rejected
                        RequestedAt = DateTime.UtcNow
                    };
                    transaction.Set(adminQueueRef, withdrawalRequest);
                });

                LoadingStatusLabel.Text = "Request Submitted!";
                await Task.Delay(1000); // Give user time to read success state
                _isProcessing = false;
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Withdrawal Error: {ex.Message}");
                LoadingOverlay.IsVisible = false;
                _isProcessing = false;
                await ShowCustomToast("Transaction Failed", ex.Message, true);
            }
        }
    }
}