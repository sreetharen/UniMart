using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using UniMart.Models;
using System;
using Microsoft.Maui.Controls;

namespace UniMart.Views
{
    public partial class RegistrationPage : ContentPage
    {
        public RegistrationPage()
        {
            InitializeComponent();
        }

        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;
            SignUpButton.IsEnabled = false;
            SignUpButton.Text = "Creating Profile...";

            string name = NameEntry.Text?.Trim();
            string email = EmailEntry.Text?.Trim();
            string password = PasswordEntry.Text;
            string confirmPassword = ConfirmPasswordEntry.Text;

            // 1. Basic Empty Field Validation
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ShowError("Please fill in all fields.");
                return;
            }

            // 2. Password Match Validation
            if (password != confirmPassword)
            {
                ShowError("Passwords do not match.");
                return;
            }

            // 3. Terms Validation
            if (!TermsCheckBox.IsChecked)
            {
                ShowError("You must agree to the Terms & Guidelines to continue.");
                return;
            }

            // 4. THE FYP FLEX: Domain-Level Security
            string lowerEmail = email.ToLower();
            if (!lowerEmail.EndsWith("@uniten.edu.my") && !lowerEmail.EndsWith("@student.uniten.edu.my"))
            {
                ShowError("Security Policy: You must use a valid UNITEN email.");
                return;
            }

            try
            {
                // 5. Create Firebase Auth Account
                var authResult = await CrossFirebaseAuth.Current.Instance.CreateUserWithEmailAndPasswordAsync(email, password);

                // 6. Create the User Profile in Firestore
                var newUserProfile = new User
                {
                    Id = authResult.User.Uid,
                    Name = name,
                    Email = email,
                    AccountStatus = "Active",
                    Wallet = new Wallet { AvailableBalance = 100.00, PendingEscrow = 0.00 }
                };

                await CrossCloudFirestore.Current.Instance
                    .Collection("users")
                    .Document(newUserProfile.Id)
                    .SetAsync(newUserProfile);

                // 7. Instant Login for the Demo
                Application.Current.MainPage = new AppShell();
            }
            catch (Exception ex)
            {
                ShowError($"System error: {ex.Message}");
            }
        }

        private void OnLoginTapped(object sender, EventArgs e)
        {
            Application.Current.MainPage = new LoginPage();
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
            SignUpButton.IsEnabled = true;
            SignUpButton.Text = "Create Account";
        }
    }
}