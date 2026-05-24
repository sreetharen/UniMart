using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;
using UniMart.Models;
using UniMart.Services;

namespace UniMart.Views
{
    public partial class BuyerHandoverPage : ContentPage
    {
        private string _offerId;
        private string _handoverToken;
        private Plugin.CloudFirestore.IListenerRegistration _offerListener;
        private TransactionEngine _transactionEngine;

        public BuyerHandoverPage(string offerId, string handoverToken)
        {
            InitializeComponent();
            _offerId = offerId;
            _handoverToken = handoverToken;
            _transactionEngine = new TransactionEngine();
            GenerateQrPayload();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            DeviceDisplay.Current.KeepScreenOn = true;
            StartEscrowListener();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DeviceDisplay.Current.KeepScreenOn = false;
            _offerListener?.Remove();
        }

        private void StartEscrowListener()
        {
            var db = CrossCloudFirestore.Current.Instance;

            _offerListener = db.Collection("offers").Document(_offerId)
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (error != null) return;

                    if (snapshot != null && snapshot.Exists)
                    {
                        var offer = snapshot.ToObject<Offer>();

                        if (offer.Status == "Disputed")
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await DisplayAlert("Escrow Frozen", "This transaction has been disputed. Please check your messages.", "OK");
                                await Navigation.PopAsync();
                            });
                            return;
                        }

                        if (offer.Status == "Completed")
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                SuccessOverlay.IsVisible = true;
                                try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
                                await Task.Delay(2500);
                                await Shell.Current.GoToAsync("//HomePage");
                            });
                        }
                    }
                });
        }

        private void GenerateQrPayload()
        {
            try
            {
                var payloadObject = new { offerId = _offerId, token = _handoverToken };
                string jsonString = JsonSerializer.Serialize(payloadObject);
                QrCodeGenerator.Value = jsonString;
            }
            catch (Exception ex)
            {
                _ = DisplayAlert("Generation Error", "Could not generate QR code.", "OK");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnReturnHomeClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//HomePage");
        }

        private async void OnDisputeClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Report Issue", "Are you sure you want to freeze this escrow? The seller will not be able to scan your code, and an admin will review the transaction.", "Yes, Freeze Funds", "Cancel");

            if (confirm)
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) return;

                bool success = await _transactionEngine.DisputeEscrow(_offerId, currentUser.Uid);

                if (success)
                {
                    await DisplayAlert("Escrow Frozen", "The transaction is paused. Please coordinate with the admin in your messages.", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Could not process the dispute. Check your connection.", "OK");
                }
            }
        }
    }
}