using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;
using UniMart.Models;

namespace UniMart.Views
{
    public partial class ChatsPage : ContentPage
    {
        private List<ChatPreview> _masterList = new();
        public ObservableCollection<ChatPreview> ActiveChats { get; set; } = new();

        private Plugin.CloudFirestore.IListenerRegistration _inboxListener;
        private string _currentUserId;
        private bool _isInitialLoading = true;

        public ChatsPage()
        {
            InitializeComponent();
            InboxList.ItemsSource = ActiveChats;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            Shell.SetTabBarIsVisible(this, true);

            StartInboxListener();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _inboxListener?.Remove();
        }

        private void StartInboxListener()
        {
            try
            {
                _currentUserId = CrossFirebaseAuth.Current.Instance.CurrentUser?.Uid;

                if (string.IsNullOrEmpty(_currentUserId))
                    return;

                var db = CrossCloudFirestore.Current.Instance;

                if (_isInitialLoading)
                {
                    LoadingSpinner.IsVisible = true;
                }

                _inboxListener?.Remove();

                _inboxListener = db.Collection("offers")
                    .WhereArrayContains("participants", _currentUserId)
                    .AddSnapshotListener((snapshot, error) =>
                    {
                        if (error != null)
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await DisplayAlert("Firestore Error", error.Message, "OK");
                            });
                            return;
                        }

                        try
                        {
                            var tempList = new List<ChatPreview>();

                            foreach (var doc in snapshot.Documents)
                            {
                                var offer = doc.ToObject<Offer>();

                                if (offer == null)
                                    continue;

                                // Skip ghost inquiries
                                if (offer.Status == "Inquiry" &&
                                    string.IsNullOrEmpty(offer.LastMessageText))
                                    continue;

                                bool isBuyer = offer.BuyerId == _currentUserId;

                                string partnerName = isBuyer
                                    ? offer.SellerName
                                    : offer.BuyerName;

                                int unread = 0;

                                if (offer.UnreadCounts != null &&
                                    offer.UnreadCounts.ContainsKey(_currentUserId))
                                {
                                    unread = offer.UnreadCounts[_currentUserId];
                                }

                                // 🔥 FIX: Intercept and sanitize System Messages so they don't clutter the Inbox row
                                string displayMessage = offer.LastMessageText ?? "Tap to chat";
                                if (displayMessage.StartsWith("[SYSTEM]") || displayMessage.Contains("✅"))
                                {
                                    // The tag on the right will say CANCELLED or COMPLETED, so we just say the status updated.
                                    displayMessage = "Status Updated";
                                }

                                tempList.Add(new ChatPreview
                                {
                                    OfferId = offer.Id ?? "",
                                    PartnerName = partnerName ?? "Member",
                                    ListingTitle = offer.ListingTitlePreview ?? "Item Inquiry",
                                    ListingImage = string.IsNullOrEmpty(offer.ListingImagePreview)
                                        ? "placeholder.png"
                                        : offer.ListingImagePreview,
                                    LastMessage = displayMessage, // Inject sanitized message
                                    LastMessageTime = offer.LastMessageTime != default
                                        ? offer.LastMessageTime
                                        : offer.Timestamp,
                                    Status = offer.Status ?? "ACTIVE",
                                    IsBuyer = isBuyer,
                                    UnreadCount = unread,
                                    RawPriceText = $"RM {offer.OfferPrice:F2}"
                                });
                            }

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                _masterList = tempList
                                    .OrderByDescending(x => x.LastMessageTime)
                                    .ToList();

                                _isInitialLoading = false;
                                LoadingSpinner.IsVisible = false;

                                ApplySearch(SearchInput.Text);
                            });
                        }
                        catch (Exception ex)
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await DisplayAlert("Snapshot Error", ex.ToString(), "OK");
                            });
                        }
                    });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Startup Error", ex.ToString(), "OK");
                });
            }
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearch(e.NewTextValue);
        }

        private void ApplySearch(string query)
        {
            IEnumerable<ChatPreview> filtered;

            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = _masterList;
            }
            else
            {
                filtered = _masterList.Where(x =>
                    (x.PartnerName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.ListingTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            ActiveChats = new ObservableCollection<ChatPreview>(filtered);

            InboxList.ItemsSource = ActiveChats;
        }

        private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection.FirstOrDefault() is ChatPreview selected)
                {
                    InboxList.SelectedItem = null;

                    await Navigation.PushAsync(new MeetupChatPage(
                        selected.OfferId,
                        selected.PartnerName,
                        selected.ListingTitle,
                        selected.ListingImage,
                        selected.RawPriceText));
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Navigation Error", ex.ToString(), "OK");
            }
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await Task.Delay(500);

            InboxRefreshView.IsRefreshing = false;
        }
    }

    public class ChatPreview
    {
        public string OfferId { get; set; }
        public string PartnerName { get; set; }
        public string ListingTitle { get; set; }
        public string ListingImage { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public string Status { get; set; }
        public bool IsBuyer { get; set; }
        public int UnreadCount { get; set; }
        public string RawPriceText { get; set; }

        public bool HasUnreadMessages => UnreadCount > 0;

        public FontAttributes MessageFontWeight =>
            HasUnreadMessages ? FontAttributes.Bold : FontAttributes.None;

        public Color MessageTextColor =>
            HasUnreadMessages ? Colors.Black : Colors.Gray;

        public string TimeAgo
        {
            get
            {
                try
                {
                    if (LastMessageTime.Year < 2000)
                        return "";

                    var localTime = LastMessageTime.ToLocalTime();

                    if (localTime.Date == DateTime.Now.Date)
                        return localTime.ToString("hh:mm tt");

                    if (localTime.Date == DateTime.Now.Date.AddDays(-1))
                        return "Yesterday";

                    return localTime.ToString("dd/MM/yy");
                }
                catch
                {
                    return "";
                }
            }
        }

        public string DisplayStatus =>
            string.IsNullOrEmpty(Status)
                ? "ACTIVE"
                : Status.ToUpper();

        public string RoleText =>
            IsBuyer ? "BUYING" : "SELLING";

        public Color RoleTextColor =>
            IsBuyer
                ? Color.FromArgb("#0056B3")
                : Color.FromArgb("#198754");

        public Color RoleBgColor =>
            IsBuyer
                ? Color.FromArgb("#E7F3FF")
                : Color.FromArgb("#D4EDDA");

        public Color StatusTextColor => DisplayStatus switch
        {
            "COMPLETED" => Color.FromArgb("#198754"),
            "CANCELLED" => Color.FromArgb("#DC3545"),
            "DISPUTED" => Color.FromArgb("#6F42C1"),
            "ACCEPTED" => Color.FromArgb("#0D6EFD"),
            _ => Color.FromArgb("#6C757D")
        };

        public Color StatusBgColor => DisplayStatus switch
        {
            "COMPLETED" => Color.FromArgb("#D1E7DD"),
            "CANCELLED" => Color.FromArgb("#F8D7DA"),
            "DISPUTED" => Color.FromArgb("#E2D9F3"),
            "ACCEPTED" => Color.FromArgb("#CFF4FC"),
            _ => Color.FromArgb("#E2E3E5")
        };
    }
}