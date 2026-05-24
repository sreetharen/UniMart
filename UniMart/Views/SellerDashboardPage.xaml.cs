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

namespace UniMart.Views;

public partial class SellerDashboardPage : ContentPage
{
    private List<SellerItemViewModel> _allMyListings = new List<SellerItemViewModel>();
    public ObservableCollection<SellerItemViewModel> DisplayedListings { get; set; } = new();
    private string _currentFilter = "All";
    private readonly TransactionEngine _transactionEngine = new();
    private Plugin.CloudFirestore.IListenerRegistration _inventoryListener;

    public SellerDashboardPage()
    {
        InitializeComponent();
        MyItemsCollectionView.ItemsSource = DisplayedListings;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, true);
        StartLiveInventorySync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _inventoryListener?.Remove();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await Task.Delay(500);
        DashboardRefreshView.IsRefreshing = false;
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

    private void StartLiveInventorySync()
    {
        LoadingIndicator.IsVisible = true;
        var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
        if (currentUser == null) return;

        _inventoryListener = CrossCloudFirestore.Current.Instance
            .Collection("listings")
            .WhereEqualsTo("sellerId", currentUser.Uid)
            .AddSnapshotListener((snapshot, error) =>
            {
                if (error != null) return;

                var tempList = new List<SellerItemViewModel>();
                foreach (var doc in snapshot.Documents)
                {
                    tempList.Add(new SellerItemViewModel(doc.ToObject<Listing>()));
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _allMyListings = tempList.OrderByDescending(x => x.ListingData.CreatedAt).ToList();
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
        DisplayedListings.Clear();
        var filtered = _currentFilter == "All"
            ? _allMyListings
            : _allMyListings.Where(x => x.Status.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered) DisplayedListings.Add(item);
        EmptyView.IsVisible = DisplayedListings.Count == 0;
    }

    private async void OnCancelReservedClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.CommandParameter as SellerItemViewModel;
        if (vm == null) return;

        bool confirm = await DisplayAlert("Cancel Deal?", "This will refund the buyer and relist the item.", "Refund & Cancel", "Back");
        if (!confirm) return;

        LoadingIndicator.IsVisible = true;
        try
        {
            var db = CrossCloudFirestore.Current.Instance;
            var offerSnapshot = await db.Collection("offers")
                                        .WhereEqualsTo("listingId", vm.ListingData.Id)
                                        .WhereEqualsTo("status", "Accepted")
                                        .GetAsync();

            var activeOffer = offerSnapshot.Documents.FirstOrDefault();
            if (activeOffer != null)
            {
                bool success = await _transactionEngine.CancelAndRefundEscrow(activeOffer.Id, vm.ListingData.SellerId);
                if (success)
                {
                    await ShowCustomToast("Refunded", "Buyer refunded and item relisted.", false);
                }
                else
                {
                    await ShowCustomToast("Error", "Transaction engine failed to process refund.", true);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowCustomToast("System Error", ex.Message, true);
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
        }
    }

    // 🔥 FIX: OnMessageBuyerClicked works universally for Reserved and Sold items.
    private async void OnMessageBuyerClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.CommandParameter as SellerItemViewModel;
        if (vm == null) return;

        var db = CrossCloudFirestore.Current.Instance;
        var offerSnap = await db.Collection("offers")
                                .WhereEqualsTo("listingId", vm.ListingData.Id)
                                .GetAsync();

        var offer = offerSnap.Documents.FirstOrDefault();
        if (offer != null) await Navigation.PushAsync(new MeetupChatPage(offer.Id));
    }

    private async void OnScanQrClicked(object sender, EventArgs e) => await Navigation.PushAsync(new SellerScannerPage());

    private async void OnDeleteListingClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.CommandParameter as SellerItemViewModel;
        if (vm == null) return;

        bool confirm = await DisplayAlert("Delete Item", "Are you sure you want to permanently remove this listing?", "Delete", "Cancel");
        if (confirm)
        {
            LoadingIndicator.IsVisible = true;
            try
            {
                await CrossCloudFirestore.Current.Instance.Collection("listings").Document(vm.ListingData.Id).DeleteAsync();
                await ShowCustomToast("Deleted", "Item removed from the marketplace.", false);
            }
            catch (Exception)
            {
                await ShowCustomToast("Error", "Could not delete item.", true);
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }

    private async void OnEditListingClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.CommandParameter as SellerItemViewModel;
        if (vm == null) return;
        await Navigation.PushAsync(new AddListingPage(vm.ListingData));
    }

    private async void OnCreateListingClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddListingPage());
    }
}

public class SellerItemViewModel
{
    public Listing ListingData { get; set; }
    public string Title => ListingData.Title;
    public double Price => ListingData.Price;
    public string Status => ListingData.Status;
    public string StatusText => Status.ToUpper();
    public string FormattedDate => ListingData.CreatedAt.ToLocalTime().ToString("MMM dd, yyyy • hh:mm tt");
    public string CoverImage => ListingData.ImageUrls?.FirstOrDefault() ?? "placeholder.png";

    public bool IsAvailable => Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
    public bool IsReserved => Status.Equals("Reserved", StringComparison.OrdinalIgnoreCase);

    // 🔥 NEW: IsSold binds to the new Sold view logic
    public bool IsSold => Status.Equals("Sold", StringComparison.OrdinalIgnoreCase) || Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);

    public Color StatusColor => Status switch
    {
        "Available" => Color.FromArgb("#198754"),
        "Reserved" => Color.FromArgb("#0D6EFD"),
        "Sold" => Color.FromArgb("#212529"),
        "Completed" => Color.FromArgb("#212529"),
        _ => Color.FromArgb("#6C757D")
    };

    public SellerItemViewModel(Listing listing) => ListingData = listing;
}