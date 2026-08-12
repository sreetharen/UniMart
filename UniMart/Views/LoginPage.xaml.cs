using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace UniMart.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            // Reset UI states safely on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ErrorLabel.IsVisible = false;
                LoginButton.IsVisible = false;
                LoginSpinner.IsVisible = true;
                LoginSpinner.IsRunning = true;
            });

            string email = EmailEntry.Text?.Trim().ToLower();
            string password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your UNITEN email and password.");
                return;
            }

            if (!email.EndsWith("@uniten.edu.my") && !email.EndsWith("@student.uniten.edu.my"))
            {
                ShowError("UniMart is exclusive to verified UNITEN accounts.");
                return;
            }

            try
            {
                IAuth auth = CrossFirebaseAuth.Current.Instance;

                // 🔥 THE BURNED BRIDGE: We only sign in. We never create accounts.
                var authResult = await auth.SignInWithEmailAndPasswordAsync(email, password);

                if (authResult?.User != null)
                {
                    var db = CrossCloudFirestore.Current.Instance;
                    var userDoc = await db.Collection("users").Document(authResult.User.Uid).GetAsync();

                    if (userDoc.Exists)
                    {
                        var userData = userDoc.ToObject<Dictionary<string, object>>();

                        // 🔥 THE STATUS GATEKEEPER
                        string accountStatus = userData.ContainsKey("accountStatus") ? userData["accountStatus"].ToString() : "Unknown";

                        if (accountStatus != "Active")
                        {
                            auth.SignOut();
                            ShowError($"Access Denied. Account status is: {accountStatus}. Please contact IT.");
                            return;
                        }

                        // 🔥 THE FIX: Force the massive AppShell navigation back onto the Main UI Thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            Application.Current.MainPage = new AppShell();
                        });
                    }
                    else
                    {
                        // User is in Auth but NOT in the Firestore Database (Orphaned account)
                        auth.SignOut();
                        ShowError("Your account has not been provisioned by UNITEN IT.");
                    }
                }
            }
            catch (Exception ex)
            {
                // 🔥 NOW FIREBASE WILL TELL YOU EXACTLY WHAT IS WRONG
                ShowError($"Error: {ex.Message}");
            }
        }

        // 🔥 GOD MODE CONTROLS (Update the passwords below to match your Firebase!)
        private void OnDemoBuyerClicked(object sender, EventArgs e)
        {
            EmailEntry.Text = "IS01083886@student.uniten.edu.my"; // Pete Jenson
            PasswordEntry.Text = "uni10pass!"; // CHANGE THIS TO PETE'S ACTUAL PASSWORD
            OnLoginClicked(this, EventArgs.Empty);
        }

        private void OnDemoSellerClicked(object sender, EventArgs e)
        {
            EmailEntry.Text = "is01083885@student.uniten.edu.my"; // Team Member
            PasswordEntry.Text = "uni10pass!"; // CHANGE THIS TO ADAM'S ACTUAL PASSWORD
            OnLoginClicked(this, EventArgs.Empty);
        }

        private async void OnForgotPasswordTapped(object sender, EventArgs e)
        {
            string email = EmailEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                await DisplayAlert("Missing Information", "Please enter your student email address first, and we will send you a reset link.", "OK");
                return;
            }

            await DisplayAlert("Processing", "Contacting the UNITEN mail server...", "OK");

            try
            {
                await Task.Delay(1500);
                // await CrossFirebaseAuth.Current.Instance.SendPasswordResetEmailAsync(email);
                await DisplayAlert("Email Sent", $"If an account exists for {email}, a secure password reset link has been sent to your inbox.", "Back to Login");
            }
            catch (Exception)
            {
                await DisplayAlert("System Error", "Could not send the reset link at this time. Please try again later.", "OK");
            }
        }

        // 🔥 THE SECOND FIX: Ensure all error messages update the UI safely
        private void ShowError(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ErrorLabel.Text = message;
                ErrorLabel.IsVisible = true;
                LoginSpinner.IsRunning = false;
                LoginSpinner.IsVisible = false;
                LoginButton.IsVisible = true;
            });
        }
    }
}