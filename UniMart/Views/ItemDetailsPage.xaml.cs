using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;

using UniMart.Models;

namespace UniMart.Views
{
    public partial class ItemDetailsPage : ContentPage, INotifyPropertyChanged
    {
        private string _currentItemId;
        private Plugin.CloudFirestore.IListenerRegistration _listingListener;
        private bool _sellerLoaded = false;
        private double _buyerBalance = 0.00;
        private bool _isProcessingTransaction = false;

        // 🔥 NEW: Storing transaction details for the overlay buttons
        private string _generatedOfferId;
        private string _generatedHandoverToken;

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- Properties ---
        private Listing _currentItem;
        public Listing CurrentItem
        {
            get => _currentItem;
            set { _currentItem = value; OnPropertyChanged(); }
        }

        private string _sellerName = "Loading Owner...";
        public string SellerName
        {
            get => _sellerName;
            set { _sellerName = value; OnPropertyChanged(); }
        }

        private string _sellerMajor = "Verifying Student Registry...";
        public string SellerMajor
        {
            get => _sellerMajor;
            set { _sellerMajor = value; OnPropertyChanged(); }
        }

        private string _sellerInitial = "?";
        public string SellerInitial
        {
            get => _sellerInitial;
            set { _sellerInitial = value; OnPropertyChanged(); }
        }

        private string _statusText = "Syncing...";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private Color _statusColor = Colors.Gray;
        public Color StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private string _mainButtonText = "Checking Status...";
        public string MainButtonText
        {
            get => _mainButtonText;
            set { _mainButtonText = value; OnPropertyChanged(); }
        }

        private bool _isMainButtonEnabled = false;
        public bool IsMainButtonEnabled
        {
            get => _isMainButtonEnabled;
            set { _isMainButtonEnabled = value; OnPropertyChanged(); }
        }

        private Color _mainButtonBgColor = Colors.Gray;
        public Color MainButtonBgColor
        {
            get => _mainButtonBgColor;
            set { _mainButtonBgColor = value; OnPropertyChanged(); }
        }

        private bool _isOwner = false;
        public bool IsOwner
        {
            get => _isOwner;
            set { _isOwner = value; OnPropertyChanged(); }
        }

        private bool _isBuyer = false;
        public bool IsBuyer
        {
            get => _isBuyer;
            set { _isBuyer = value; OnPropertyChanged(); }
        }

        private bool _hasImages = false;
        public bool HasImages
        {
            get => _hasImages;
            set { _hasImages = value; OnPropertyChanged(); }
        }

        private bool _hasNoImages = true;
        public bool HasNoImages
        {
            get => _hasNoImages;
            set { _hasNoImages = value; OnPropertyChanged(); }
        }

        public ItemDetailsPage(Listing item)
        {
            InitializeComponent();
            _currentItemId = item.Id;
            CurrentItem = item;
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _isProcessingTransaction = false;

            await LoadBuyerWalletBalance();
            await FetchSellerDetails();
            StartLiveListingSync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _listingListener?.Dispose();
            _listingListener = null;
        }

        private void StartLiveListingSync()
        {
            if (_listingListener != null) return;

            _listingListener = CrossCloudFirestore.Current.Instance
                .Collection("listings")
                .Document(_currentItemId)
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (error != null || snapshot == null || !snapshot.Exists) return;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CurrentItem = snapshot.ToObject<Listing>();

                        if (DescriptionSection != null)
                            DescriptionSection.IsVisible = !string.IsNullOrWhiteSpace(CurrentItem.Description);

                        EvaluateStateMatrix();
                    });
                });
        }

        private void EvaluateStateMatrix()
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null || CurrentItem == null) return;

            IsOwner = currentUser.Uid == CurrentItem.SellerId;
            IsBuyer = !IsOwner;
            HasImages = CurrentItem.ImageUrls != null && CurrentItem.ImageUrls.Count > 0;
            HasNoImages = !HasImages;

            Color primaryColor = Application.Current.Resources.TryGetValue("Primary", out var p) ? (Color)p : Color.FromArgb("#0056B3");

            if (CurrentItem.Status == "Available")
            {
                StatusText = "Available";
                StatusColor = Color.FromArgb("#28A745");

                if (_buyerBalance < CurrentItem.Price)
                {
                    MainButtonText = "Insufficient Funds";
                    IsMainButtonEnabled = false;
                    MainButtonBgColor = Color.FromArgb("#A0C4FF");
                }
                else
                {
                    MainButtonText = "Reserve & Lock";
                    IsMainButtonEnabled = true;
                    MainButtonBgColor = primaryColor;
                }
            }
            else if (CurrentItem.Status == "Reserved")
            {
                StatusText = "Reserved";
                StatusColor = Color.FromArgb("#FFC107");
                MainButtonText = "Item On Hold";
                IsMainButtonEnabled = false;
                MainButtonBgColor = Color.FromArgb("#6E6E6E");
            }
            else
            {
                StatusText = "Sold Out";
                StatusColor = Color.FromArgb("#DC3545");
                MainButtonText = "Sold Out";
                IsMainButtonEnabled = false;
                MainButtonBgColor = Color.FromArgb("#2B2B2B");
            }
        }

        private async Task LoadBuyerWalletBalance()
        {
            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) return;

                var userDoc = await CrossCloudFirestore.Current.Instance.Collection("users").Document(currentUser.Uid).GetAsync();
                if (userDoc.Exists)
                {
                    var userData = userDoc.ToObject<Dictionary<string, object>>();
                    if (userData != null && userData.ContainsKey("wallet"))
                    {
                        var walletData = userData["wallet"] as Dictionary<string, object>;
                        if (walletData != null && walletData.ContainsKey("availableBalance"))
                        {
                            _buyerBalance = Convert.ToDouble(walletData["availableBalance"]);
                        }
                    }
                }
            }
            catch { }
        }

        private async Task FetchSellerDetails()
        {
            if (_sellerLoaded) return;

            try
            {
                var userDoc = await CrossCloudFirestore.Current.Instance.Collection("users").Document(CurrentItem.SellerId).GetAsync();

                if (userDoc.Exists)
                {
                    var userData = userDoc.ToObject<Dictionary<string, object>>();

                    SellerName = userData.ContainsKey("name") && userData["name"] != null && !string.IsNullOrWhiteSpace(userData["name"].ToString())
                      ? userData["name"].ToString()
                      : "UNITEN Student";

                    SellerMajor = userData.ContainsKey("major") && userData["major"] != null && !string.IsNullOrWhiteSpace(userData["major"].ToString())
                      ? userData["major"].ToString()
                      : "Student";

                    SellerInitial = string.IsNullOrEmpty(SellerName) ? "?" : SellerName.Substring(0, 1).ToUpper();
                    _sellerLoaded = true;
                }
            }
            catch (Exception ex) { Console.WriteLine($"Identity Engine Fault: {ex.Message}"); }
        }

        private async Task<string> FetchBuyerName(string uid)
        {
            try
            {
                var buyerDoc = await CrossCloudFirestore.Current.Instance.Collection("users").Document(uid).GetAsync();
                if (buyerDoc.Exists)
                {
                    var userData = buyerDoc.ToObject<Dictionary<string, object>>();
                    if (userData != null && userData.ContainsKey("name") && userData["name"] != null)
                        return userData["name"].ToString();
                }
            }
            catch { }
            return "Student";
        }

        private async void OnAskSellerClicked(object sender, EventArgs e)
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null) return;

            if (currentUser.Uid == CurrentItem.SellerId)
            {
                await DisplayAlert("Note", "This is your own listing.", "OK");
                return;
            }

            var chatButton = sender as Button;
            if (chatButton != null) chatButton.IsEnabled = false;

            string inquiryId = $"inquiry_{CurrentItem.Id}_{currentUser.Uid}";

            try
            {
                string buyerName = await FetchBuyerName(currentUser.Uid);

                await Navigation.PushAsync(new MeetupChatPage(
                    inquiryId,
                    CurrentItem.Id,
                    currentUser.Uid,
                    buyerName,
                    CurrentItem.SellerId,
                    SellerName,
                    CurrentItem.Title,
                    CurrentItem.CoverImage,
                    CurrentItem.Price
                ));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                if (chatButton != null) chatButton.IsEnabled = true;
            }
        }

        private async void OnBuyClicked(object sender, EventArgs e)
        {
            if (_isProcessingTransaction) return;

            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null || currentUser.Uid == CurrentItem.SellerId) return;

            bool confirmPurchase = await DisplayAlert("Confirm Escrow Lock", $"Lock RM {CurrentItem.Price:F2} in Escrow?", "Yes, Lock", "Cancel");
            if (!confirmPurchase) return;

            _isProcessingTransaction = true;

            // 🔥 ACTIVATE THE PREMIUM PROCESSING OVERLAY
            StatusOverlay.IsVisible = true;
            ProcessingStateContainer.IsVisible = true;
            SuccessStateContainer.IsVisible = false;

            try
            {
                var db = CrossCloudFirestore.Current.Instance;
                var listingRef = db.Collection("listings").Document(CurrentItem.Id);
                var buyerRef = db.Collection("users").Document(currentUser.Uid);
                var receiptRef = db.Collection("transactions").Document();
                string offerId = $"inquiry_{CurrentItem.Id}_{currentUser.Uid}";
                var offerRef = db.Collection("offers").Document(offerId);

                string generatedHandoverToken = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
                string buyerName = await FetchBuyerName(currentUser.Uid);

                await db.RunTransactionAsync(transaction =>
                {
                    var listingSnap = transaction.Get(listingRef);
                    var buyerSnap = transaction.Get(buyerRef);
                    var offerSnap = transaction.Get(offerRef);

                    var listing = listingSnap.ToObject<Listing>();
                    var buyer = buyerSnap.ToObject<User>();

                    if (listing.Status != "Available") throw new Exception("This item was just reserved!");
                    if (buyer.Wallet.AvailableBalance < CurrentItem.Price) throw new Exception("Insufficient balance.");

                    transaction.Update(listingRef, "status", "Reserved");
                    transaction.Update(listingRef, "soldToBuyerId", currentUser.Uid);
                    transaction.Update(buyerRef, "wallet.availableBalance", FieldValue.Increment(-CurrentItem.Price));
                    transaction.Update(buyerRef, "wallet.pendingEscrow", FieldValue.Increment(CurrentItem.Price));

                    DateTime now = DateTime.UtcNow;

                    if (offerSnap.Exists)
                    {
                        transaction.Update(offerRef, "status", "Accepted");
                        transaction.Update(offerRef, "offerPrice", CurrentItem.Price);
                        transaction.Update(offerRef, "handoverToken", generatedHandoverToken);
                        transaction.Update(offerRef, "buyerReceiptId", receiptRef.Id);
                        transaction.Update(offerRef, "escrowState", new EscrowState { Status = "Held", HeldAmount = CurrentItem.Price });
                        transaction.Update(offerRef, "lastMessageText", "Purchase confirmed! Let's arrange a meetup.");
                        transaction.Update(offerRef, "lastMessageTime", now);
                        transaction.Update(offerRef, $"unreadCounts.{CurrentItem.SellerId}", FieldValue.Increment(1));
                    }
                    else
                    {
                        var offerData = new Offer
                        {
                            Id = offerId,
                            ListingId = CurrentItem.Id,
                            BuyerId = currentUser.Uid,
                            SellerId = CurrentItem.SellerId,
                            BuyerName = buyerName,
                            SellerName = SellerName,
                            UnreadCounts = new Dictionary<string, int> { { CurrentItem.SellerId, 1 }, { currentUser.Uid, 0 } },
                            Participants = new List<string> { currentUser.Uid, CurrentItem.SellerId },
                            ListingTitlePreview = CurrentItem.Title,
                            ListingImagePreview = CurrentItem.CoverImage,
                            LastMessageText = "Purchase confirmed! Let's arrange a meetup.",
                            LastMessageTime = now,
                            Status = "Accepted",
                            OfferPrice = CurrentItem.Price,
                            HandoverToken = generatedHandoverToken,
                            BuyerReceiptId = receiptRef.Id,
                            EscrowState = new EscrowState { Status = "Held", HeldAmount = CurrentItem.Price },
                            Timestamp = now
                        };
                        transaction.Set(offerRef, offerData);
                    }

                    transaction.Set(receiptRef, new TransactionRecord
                    {
                        Id = receiptRef.Id,
                        UserId = currentUser.Uid,
                        Amount = CurrentItem.Price,
                        Type = "EscrowLock",
                        Description = $"Reserved: {listing.Title}",
                        Date = now
                    });
                });

                // 🔥 STORE THE GLOBALS FOR THE BUTTONS
                _generatedOfferId = offerId;
                _generatedHandoverToken = generatedHandoverToken;

                // 🔥 TRANSITION TO THE SUCCESS OVERLAY
                ProcessingStateContainer.IsVisible = false;
                SuccessStateContainer.IsVisible = true;
            }
            catch (Exception ex)
            {
                _isProcessingTransaction = false;
                StatusOverlay.IsVisible = false;
                await DisplayAlert("Transaction Failed", ex.Message, "OK");
            }
        }

        // 🔥 NEW INTEGRATED ACTION: View QR directly from the overlay
        private async void OnViewQRClicked(object sender, EventArgs e)
        {
            // Close the overlay to keep the back-stack clean, then push to QR page
            StatusOverlay.IsVisible = false;
            await Navigation.PushAsync(new BuyerHandoverPage(_generatedOfferId, _generatedHandoverToken));
        }

        // 🔥 NEW INTEGRATED ACTION: Return Home directly from the overlay
        private async void OnBackToHomeClicked(object sender, EventArgs e)
        {
            StatusOverlay.IsVisible = false;
            await Shell.Current.GoToAsync("///HomePage");
        }

        private async void OnReportItemClicked(object sender, EventArgs e)
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
            if (currentUser == null) return;

            string action = await DisplayActionSheet("Reason for Reporting", "Cancel", null,
                "Prohibited Content",
                "Academic Malpractice",
                "Scam / Fraud",
                "Inappropriate Media");

            if (string.IsNullOrEmpty(action) || action == "Cancel") return;

            try
            {
                var db = CrossCloudFirestore.Current.Instance;
                string reportId = $"report_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                var reportData = new Dictionary<string, object>
                {
                    { "reportId", reportId },
                    { "listingId", CurrentItem.Id },
                    { "listingTitle", CurrentItem.Title },
                    { "reporterId", currentUser.Uid },
                    { "reason", action },
                    { "timestamp", DateTime.UtcNow }
                };

                await db.Collection("reports").Document(reportId).SetAsync(reportData);
                await db.Collection("listings").Document(CurrentItem.Id)
                    .UpdateAsync("reportCount", FieldValue.Increment(1));

                await DisplayAlert("Report Submitted", "Thank you. This listing has been reported to administration.", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not submit report: {ex.Message}", "OK");
            }
        }

        private async void OnBackTapped(object sender, EventArgs e) => await Navigation.PopAsync();
    }
}