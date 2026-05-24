using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;
using System.Text.Json;
using UniMart.Models;
using UniMart.Services;
using ZXing.Net.Maui;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace UniMart.Views
{
    public partial class SellerScannerPage : ContentPage
    {
        private bool _isProcessing = false;
        private TransactionEngine _transactionEngine;

        public SellerScannerPage()
        {
            InitializeComponent();
            _transactionEngine = new TransactionEngine();

            CameraScanner.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false
            };
        }

        // 🔥 FIX: Ensure TabBar stays dead while camera is active
        protected override void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetTabBarIsVisible(this, false);
        }

        // 🔥 FIX: Back button logic
        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (_isProcessing) return; // Prevent backing out while talking to Firebase
            await Navigation.PopAsync();
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

            // 🔥 FIX: Dropped from 2500ms to 800ms for a snappy, premium exit!
            await Task.Delay(800);

            await CustomAlertBox.TranslateTo(0, -150, 300, Easing.CubicIn);
        }

        private async void CameraScanner_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
        {
            if (_isProcessing) return;

            var firstBarcode = e.Results?.FirstOrDefault();
            if (firstBarcode == null) return;

            _isProcessing = true;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CameraScanner.IsDetecting = false;
                ProcessingOverlay.IsVisible = true;
            });

            await ProcessEscrowRelease(firstBarcode.Value);
        }

        private async Task ProcessEscrowRelease(string qrPayloadJson)
        {
            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) throw new Exception("You must be logged in to scan.");

                var payload = JsonSerializer.Deserialize<QrPayload>(qrPayloadJson);
                if (payload == null || string.IsNullOrEmpty(payload.offerId) || string.IsNullOrEmpty(payload.token))
                {
                    throw new Exception("Invalid UniMart QR Code format.");
                }

                bool success = await _transactionEngine.FinalizeHandover(payload.offerId, payload.token, currentUser.Uid);

                if (success)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ProcessingOverlay.IsVisible = false;

                        // 🔥 FIX: Show the success banner, wait a second, then pop the page automatically
                        await ShowCustomToast("Handover Complete!", "Funds safely transferred to your wallet.", false);

                        await Navigation.PopAsync();
                    });
                }
                else
                {
                    throw new Exception("Security mismatch or unauthorized scan.");
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ProcessingOverlay.IsVisible = false;

                    // 🔥 FIX: Show error toast and reactivate the camera
                    await ShowCustomToast("Verification Failed", ex.Message, true);

                    _isProcessing = false;
                    CameraScanner.IsDetecting = true;
                });
            }
        }
    }

    public class QrPayload
    {
        public string offerId { get; set; }
        public string token { get; set; }
    }
}