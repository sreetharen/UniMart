using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;
using UniMart.Models;

namespace UniMart.Views
{
    public partial class HomePage : ContentPage
    {
        #region Fields & Properties

        private List<Listing> _masterDatabaseCache = new List<Listing>();
        private Plugin.CloudFirestore.IListenerRegistration _liveFeedListener;
        private string _currentCategory = "All Items";
        private bool _isNavigating = false;

        public ObservableCollection<Listing> VisibleListings { get; set; } = new ObservableCollection<Listing>();
        public ObservableCollection<Listing> SearchSuggestions { get; set; } = new ObservableCollection<Listing>();

        private bool _hasListings = true;
        public bool HasListings
        {
            get => _hasListings;
            set { _hasListings = value; OnPropertyChanged(); }
        }

        private bool _hasEmptyState = false;
        public bool HasEmptyState
        {
            get => _hasEmptyState;
            set { _hasEmptyState = value; OnPropertyChanged(); }
        }

        #endregion

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;
            ItemsCollectionView.ItemsSource = VisibleListings;
            SearchSuggestionsCollectionView.ItemsSource = SearchSuggestions;
        }

        #region Lifecycle Methods

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _isNavigating = false;
            Shell.SetTabBarIsVisible(this, true);

            await LoadUserWallet();
            StartLiveMarketplaceFeed();
        }

        #endregion

        #region Data Loading & Synchronization

        private void StartLiveMarketplaceFeed()
        {
            if (_liveFeedListener != null) return;

            MainThread.BeginInvokeOnMainThread(() => {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
            });

            _liveFeedListener = CrossCloudFirestore.Current.Instance
                .Collection("listings")
                .WhereEqualsTo("status", "Available")
                .AddSnapshotListener((snapshot, error) =>
                {
                    if (error != null) return;

                    if (snapshot != null)
                    {
                        _masterDatabaseCache.Clear();
                        foreach (var document in snapshot.Documents)
                        {
                            _masterDatabaseCache.Add(document.ToObject<Listing>());
                        }

                        ApplyFilters(forceScrollToTop: false);

                        MainThread.BeginInvokeOnMainThread(() => {
                            LoadingIndicator.IsRunning = false;
                            LoadingIndicator.IsVisible = false;
                        });
                    }
                });
        }

        private async Task LoadUserWallet()
        {
            try
            {
                var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                if (currentUser == null) return;

                var userDoc = await CrossCloudFirestore.Current.Instance.Collection("users").Document(currentUser.Uid).GetAsync();
                if (userDoc.Exists)
                {
                    var userData = userDoc.ToObject<Dictionary<string, object>>();
                    if (userData.TryGetValue("wallet", out var w) && w is Dictionary<string, object> walletData)
                    {
                        if (walletData.TryGetValue("availableBalance", out var balanceObj))
                        {
                            double balance = Convert.ToDouble(balanceObj);
                            MainThread.BeginInvokeOnMainThread(() => WalletBalanceLabel.Text = balance.ToString("0.00"));
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Wallet Sync Error: {ex.Message}"); }
        }

        #endregion

        #region Search Logic

        private void OnSearchEntryFocused(object sender, FocusEventArgs e)
        {
            SearchOverlay.IsVisible = true;
            CancelSearchButton.IsVisible = true;
            Shell.SetTabBarIsVisible(this, false);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue?.ToLower() ?? string.Empty;
            SearchSuggestions.Clear();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var suggestions = _masterDatabaseCache
                    .Where(x => x.Title?.ToLower().Contains(searchText) == true)
                    .Take(8);

                foreach (var s in suggestions) SearchSuggestions.Add(s);
            }
        }

        private void OnSearchEntryCompleted(object sender, EventArgs e) => CloseSearch();

        private void OnCancelSearchClicked(object sender, EventArgs e)
        {
            SearchEntry.Text = string.Empty;
            CloseSearch();
        }

        private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Listing selectedListing)
            {
                SearchEntry.Text = selectedListing.Title;
                CloseSearch();
            }
        }

        private void CloseSearch()
        {
            SearchEntry.Unfocus();
            SearchOverlay.IsVisible = false;
            CancelSearchButton.IsVisible = false;
            Shell.SetTabBarIsVisible(this, true);
            ApplyFilters(forceScrollToTop: true);
        }

        #endregion

        #region UI & Filter Logic

        private void OnCategoryTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string category && sender is Border tappedBorder)
            {
                _currentCategory = category;

                // Reset all categories
                foreach (var child in CategoryStack.Children.OfType<Border>())
                {
                    child.BackgroundColor = Colors.Transparent;
                    child.StrokeThickness = 1;
                    if (child.Content is Label lbl) lbl.TextColor = (Color)Application.Current.Resources["Gray500"];
                }

                // Activate selected
                tappedBorder.BackgroundColor = (Color)Application.Current.Resources["Primary"];
                tappedBorder.StrokeThickness = 0;
                if (tappedBorder.Content is Label activeLabel) activeLabel.TextColor = Colors.White;

                ApplyFilters();
            }
        }

        private void ApplyFilters(bool forceScrollToTop = false)
        {
            var searchText = SearchEntry.Text?.ToLower() ?? string.Empty;

            var filtered = _masterDatabaseCache.AsEnumerable();

            if (_currentCategory != "All Items")
                filtered = filtered.Where(x => x.Category == _currentCategory);

            if (!string.IsNullOrWhiteSpace(searchText))
                filtered = filtered.Where(x =>
                    x.Title?.ToLower().Contains(searchText) == true ||
                    x.Description?.ToLower().Contains(searchText) == true);

            var sortedList = filtered.OrderByDescending(x => x.CreatedAt).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                VisibleListings.Clear();
                foreach (var item in sortedList) VisibleListings.Add(item);

                HasListings = VisibleListings.Count > 0;
                HasEmptyState = !HasListings;

                if (forceScrollToTop && VisibleListings.Count > 0)
                    ItemsCollectionView.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
            });
        }

        #endregion

        #region Event Handlers

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await LoadUserWallet();
            FeedRefreshView.IsRefreshing = false;
        }

        private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_isNavigating) return;
            if (e.CurrentSelection.FirstOrDefault() is Listing selectedListing)
            {
                _isNavigating = true;
                ItemsCollectionView.SelectedItem = null;
                await Navigation.PushAsync(new ItemDetailsPage(selectedListing));
            }
        }

        private async void OnSellNowClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//SellPage");

        #endregion
    }
}