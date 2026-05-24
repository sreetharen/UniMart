using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using UniMart.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace UniMart.Views
{
    public partial class ProfilePage : ContentPage
    {
        // 🔥 FIX: Real-time listener instance
        private Plugin.CloudFirestore.IListenerRegistration _profileListener;

        public ProfilePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartRealTimeProfileSync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _profileListener?.Remove(); // Cleans up memory when you leave the tab
        }

        // 🔥 NEW: Premium Toast Engine
        private async Task ShowCustomToast(string title, string message, bool isError = false)
        {
            if (CustomAlertBox == null) return;
            AlertTitle.Text = title;
            AlertMessage.Text = message;
            AlertBorder.BackgroundColor = isError ? Color.FromArgb("#DC3545") : Color.FromArgb("#198754");
            AlertIcon.Text = isError ? "✕" : "✓";

            await CustomAlertBox.TranslateTo(0, 40, 400, Easing.CubicOut);
            await Task.Delay(3500);
            await CustomAlertBox.TranslateTo(0, -150, 300, Easing.CubicIn);
        }

        // 🔥 FIX: Swapped GetAsync for a live listener so financial data never desyncs
        private void StartRealTimeProfileSync()
        {
            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) return;

                EmailLabel.Text = currentUser.Email;

                _profileListener = CrossCloudFirestore.Current.Instance
                    .Collection("users")
                    .Document(currentUser.Uid)
                    .AddSnapshotListener((snapshot, error) =>
                    {
                        if (error != null) return;

                        if (snapshot != null && snapshot.Exists)
                        {
                            var userData = snapshot.ToObject<User>();

                            if (userData != null)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    string fullName = userData.Name ?? "UNITEN Student";
                                    NameLabel.Text = fullName;

                                    MajorLabel.Text = userData.Major ?? "Course not set";

                                    if (!string.IsNullOrEmpty(fullName))
                                    {
                                        AvatarLabel.Text = fullName.Substring(0, 1).ToUpper();
                                    }

                                    double balance = userData.Wallet?.AvailableBalance ?? 0.00;
                                    WalletBalanceLabel.Text = $"RM {balance:F2}";
                                });
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProfilePage] Critical Error: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NameLabel.Text = "UNITEN Student";
                });
            }
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to securely sign out?", "Yes", "Cancel");
            if (confirm)
            {
                CrossFirebaseAuth.Current.Instance.SignOut();

                // Change this line:
                var loginPage = new LoginPage();
                NavigationPage.SetHasNavigationBar(loginPage, false); // Hide the header
                Application.Current.MainPage = new NavigationPage(loginPage);
            }
        }

        private async void OnSellerDashboardClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new SellerDashboardPage());

        private async void OnBuyerOrdersClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new BuyerOrdersPage());

        private async void OnDeleteAccountClicked(object sender, EventArgs e)
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null) return;

            try
            {
                var activeListings = await CrossCloudFirestore.Current.Instance.Collection("listings")
                    .WhereEqualsTo("sellerId", currentUser.Uid)
                    .GetAsync();

                if (activeListings.Count > 0)
                {
                    // Error message replaced with Toast
                    await ShowCustomToast("Action Required", $"Please remove your {activeListings.Count} active items via Seller Dashboard first.", true);
                    return;
                }

                bool finalConfirm = await DisplayAlert("Close Account", "This action will permanently delete your identity, balance, and history. There is no recovery. Proceed?", "Delete Account", "Cancel");

                if (finalConfirm)
                {
                    await CrossCloudFirestore.Current.Instance.Collection("users").Document(currentUser.Uid).DeleteAsync();
                    await currentUser.DeleteAsync();
                    Application.Current.MainPage = new NavigationPage(new LoginPage());
                }
            }
            catch (Exception)
            {
                // Re-auth security prompt replaced with Toast
                await ShowCustomToast("Session Expired", "Please log out and back in to verify your identity before deleting.", true);
            }
        }
    }
}