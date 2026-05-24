using Microsoft.Maui.Controls;
using System;

namespace UniMart.Views
{
    public partial class PaymentSuccessPage : ContentPage
    {
        private string _offerId;
        private string _handoverToken;

        // We receive the data passed from the ItemDetailsPage
        public PaymentSuccessPage(string offerId, string handoverToken, double amount)
        {
            InitializeComponent();

            _offerId = offerId;
            _handoverToken = handoverToken;

            // Format the amount for the UI
            AmountLabel.Text = amount.ToString("F2");
        }

        // Disable the hardware back button so they can't accidentally go back to the payment process
        protected override bool OnBackButtonPressed()
        {
            return true;
        }

        private async void OnViewQRClicked(object sender, EventArgs e)
        {
            // Proceed to the QR code page
            await Navigation.PushAsync(new BuyerHandoverPage(_offerId, _handoverToken));
        }

        private async void OnBackToHomeClicked(object sender, EventArgs e)
        {
            // Pop back to the root (Home Page) to keep shopping
            await Navigation.PopToRootAsync();
        }
    }
}