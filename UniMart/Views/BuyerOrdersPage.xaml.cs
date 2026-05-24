using Plugin.FirebaseAuth;
using Plugin.CloudFirestore;
using UniMart.Models;
using UniMart.Services;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace UniMart.Views
{
    public partial class BuyerOrdersPage : ContentPage
    {
        private List<BuyerOrderViewModel> _allMyOrders = new List<BuyerOrderViewModel>();
        public ObservableCollection<BuyerOrderViewModel> DisplayedOrders { get; set; } = new();
        private Plugin.CloudFirestore.IListenerRegistration _ordersListener;
        private readonly TransactionEngine _transactionEngine = new();
        private string _currentFilter = "All";
        private string _userId;

        public BuyerOrdersPage()
        {
            InitializeComponent();
            OrdersCollectionView.ItemsSource = DisplayedOrders;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Re-enforce Shell visibility to prevent layout ghost-space
            Shell.SetTabBarIsVisible(this, true);
            Shell.SetNavBarIsVisible(this, false);
            this.ForceLayout();

            StartLiveOrdersListener();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _ordersListener?.Remove();
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

        private void StartLiveOrdersListener()
        {
            _userId = CrossFirebaseAuth.Current.Instance.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(_userId)) return;

            LoadingIndicator.IsVisible = true;

            _ordersListener = CrossCloudFirestore.Current.Instance
                .Collection("offers")
                .WhereEqualsTo("buyerId", _userId)
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (error != null) return;

                    var tempList = new List<BuyerOrderViewModel>();
                    foreach (var doc in snapshot.Documents)
                    {
                        var offer = doc.ToObject<Offer>();
                        if (offer.Status == "Inquiry") continue;
                        tempList.Add(new BuyerOrderViewModel { OfferData = offer });
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Sort by the latest action to ensure freshest transactions are on top
                        _allMyOrders = tempList.OrderByDescending(x => x.OfferData.LastMessageTime).ToList();
                        ApplyFilter();
                        LoadingIndicator.IsVisible = false;
                    });
                });
        }

        private void OnTabTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string filter)
            {
                _currentFilter = filter;
                ResetAllTabs();
                var tapped = (VerticalStackLayout)sender;
                ((Label)tapped.Children[0]).TextColor = (Color)Application.Current.Resources["Primary"];
                ((BoxView)tapped.Children[1]).Color = (Color)Application.Current.Resources["Primary"];
                ApplyFilter();
            }
        }

        private void ResetAllTabs()
        {
            foreach (var child in PillContainer.Children)
            {
                if (child is VerticalStackLayout layout)
                {
                    ((Label)layout.Children[0]).TextColor = (Color)Application.Current.Resources["Gray500"];
                    ((BoxView)layout.Children[1]).Color = Colors.Transparent;
                }
            }
        }

        private void ApplyFilter()
        {
            DisplayedOrders.Clear();
            var filtered = _currentFilter switch
            {
                "Active" => _allMyOrders.Where(x => x.IsActive).ToList(),
                "History" => _allMyOrders.Where(x => !x.IsActive).ToList(),
                _ => _allMyOrders
            };
            foreach (var item in filtered) DisplayedOrders.Add(item);
            EmptyView.IsVisible = DisplayedOrders.Count == 0;
        }

        private async void OnCancelOrderClicked(object sender, EventArgs e)
        {
            var vm = (sender as Button)?.CommandParameter as BuyerOrderViewModel;
            if (vm == null || !vm.CanCancel) return;

            bool confirm = await DisplayAlert("Cancel Transaction", $"Are you sure you want to refund RM {vm.DisplayPrice:F2}?", "Refund", "Back");
            if (confirm)
            {
                LoadingIndicator.IsVisible = true;
                bool success = await _transactionEngine.CancelAndRefundEscrow(vm.OfferData.Id, _userId);
                LoadingIndicator.IsVisible = false;

                if (!success)
                {
                    await ShowCustomToast("Cancellation Failed", "Unable to process the refund. Please try again.", true);
                }
                else
                {
                    await ShowCustomToast("Refunded", "Transaction cancelled successfully.", false);
                }
            }
        }

        // 🔥 FIX: OnFileDisputeClicked has been completely removed to streamline the Escrow engine

        private async void OnViewQrClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new BuyerHandoverPage(((BuyerOrderViewModel)((Button)sender).CommandParameter).OfferData.Id, ((BuyerOrderViewModel)((Button)sender).CommandParameter).OfferData.HandoverToken));

        private async void OnMessageSellerClicked(object sender, EventArgs e) =>
            await Navigation.PushAsync(new MeetupChatPage(((BuyerOrderViewModel)((Button)sender).CommandParameter).OfferData.Id));

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await Task.Delay(500);
            OrdersRefreshView.IsRefreshing = false;
        }

        private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
    }

    public class BuyerOrderViewModel
    {
        public Offer OfferData { get; set; }
        public string ItemTitle => OfferData?.ListingTitlePreview ?? "Item";
        public string CoverImage => OfferData?.ListingImagePreview ?? "placeholder.png";
        public double DisplayPrice => OfferData?.OfferPrice ?? 0.0;

        // 🔥 FIX: Now uses LastMessageTime to ensure the date updates when the status changes (e.g. from Cancelled to Reserved again)
        public string FullDateTime => (OfferData?.LastMessageTime != default ? OfferData.LastMessageTime : OfferData.Timestamp).ToLocalTime().ToString("MMM dd • hh:mm tt");

        public string StatusText => OfferData?.Status?.ToUpper() ?? "ACTIVE";

        public Color StatusColor => StatusText switch
        {
            "ACCEPTED" => Color.FromArgb("#0D6EFD"),
            "CANCELLED" => Colors.Red,
            "COMPLETED" => Color.FromArgb("#198754"),
            _ => Colors.Gray
        };

        public bool IsActive => StatusText == "ACCEPTED";
        public bool CanViewQr => StatusText == "ACCEPTED";
        public bool CanCancel => StatusText == "ACCEPTED";

        // 🔥 FIX: Chat is now universally available regardless of the order status.
        public bool CanMessage => true;
    }
}