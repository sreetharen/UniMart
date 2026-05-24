using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;

using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;

using UniMart.Config;
using UniMart.Models;

namespace UniMart.Views;

public partial class MeetupChatPage : ContentPage
{
    private string _offerId;
    private string _currentUserId;
    private string _partnerId;

    // Memory fields for Lazy Initialization
    private string _listingId;
    private string _buyerId;
    private string _buyerName;
    private string _sellerId;
    private string _sellerName;
    private string _listingTitle;
    private string _listingImage;
    private double _offerPrice;

    private Plugin.CloudFirestore.IListenerRegistration _chatListener;
    private Plugin.CloudFirestore.IListenerRegistration _offerStatusListener;
    public ObservableCollection<Message> ChatMessages { get; set; } = new();

    private bool _isPartnerRead;
    public bool IsPartnerRead
    {
        get => _isPartnerRead;
        set
        {
            if (_isPartnerRead != value)
            {
                _isPartnerRead = value;
                OnPropertyChanged(nameof(IsPartnerRead));
            }
        }
    }

    // 🔥 CONSTRUCTOR 1: Fast-load version used by the Inbox (ChatsPage)
    public MeetupChatPage(string offerId, string partnerName, string listingTitle, string listingImage, string priceText)
    {
        InitializeComponent();
        _offerId = offerId;
        ChatMessagesList.ItemsSource = ChatMessages;
        _currentUserId = CrossFirebaseAuth.Current.Instance.CurrentUser?.Uid;

        PartnerNameLabel.Text = partnerName;
        PartnerInitialLabel.Text = string.IsNullOrEmpty(partnerName) ? "?" : partnerName.Substring(0, 1).ToUpper();
        ItemTitleLabel.Text = listingTitle;
        ItemThumbnail.Source = listingImage;
        ItemPriceLabel.Text = priceText;

        BindingContext = this;
    }

    // 🛡️ CONSTRUCTOR 2: Compatibility Shield used by Dashboard and Orders
    public MeetupChatPage(string offerId)
    {
        InitializeComponent();
        _offerId = offerId;
        ChatMessagesList.ItemsSource = ChatMessages;
        _currentUserId = CrossFirebaseAuth.Current.Instance.CurrentUser?.Uid;
        BindingContext = this;

        _ = LoadFallbackContextAsync();
    }

    // 🚀 CONSTRUCTOR 3: Lazy Initialization for New Inquiries (From ItemDetailsPage)
    public MeetupChatPage(string offerId, string listingId, string buyerId, string buyerName, string sellerId, string sellerName, string listingTitle, string listingImage, double offerPrice)
    {
        InitializeComponent();
        _offerId = offerId;
        _listingId = listingId;
        _buyerId = buyerId;
        _buyerName = buyerName;
        _sellerId = sellerId;
        _sellerName = sellerName;
        _listingTitle = listingTitle;
        _listingImage = listingImage;
        _offerPrice = offerPrice;

        _currentUserId = CrossFirebaseAuth.Current.Instance.CurrentUser?.Uid;
        _partnerId = sellerId; // Because the current user is the buyer initiating the chat

        ChatMessagesList.ItemsSource = ChatMessages;
        PartnerNameLabel.Text = sellerName;
        PartnerInitialLabel.Text = string.IsNullOrEmpty(sellerName) ? "?" : sellerName.Substring(0, 1).ToUpper();
        ItemTitleLabel.Text = listingTitle;
        ItemThumbnail.Source = listingImage;
        ItemPriceLabel.Text = $"RM {offerPrice:F2}";

        BindingContext = this;
    }

    private async Task LoadFallbackContextAsync()
    {
        try
        {
            var db = CrossCloudFirestore.Current.Instance;
            var offerDoc = await db.Collection("offers").Document(_offerId).GetAsync();
            if (!offerDoc.Exists) return;

            var offer = offerDoc.ToObject<Offer>();
            _partnerId = (_currentUserId == offer.BuyerId) ? offer.SellerId : offer.BuyerId;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ItemPriceLabel.Text = $"RM {offer.OfferPrice:F2}";
            });

            var listingDoc = await db.Collection("listings").Document(offer.ListingId).GetAsync();
            if (listingDoc.Exists)
            {
                var listing = listingDoc.ToObject<Listing>();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ItemTitleLabel.Text = listing.Title;
                    ItemThumbnail.Source = listing.ImageUrls?.FirstOrDefault();
                });
            }

            var partnerDoc = await db.Collection("users").Document(_partnerId).GetAsync();
            var partnerName = partnerDoc.ToObject<User>()?.Name ?? "Member";

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PartnerNameLabel.Text = partnerName;
                PartnerInitialLabel.Text = string.IsNullOrEmpty(partnerName) ? "?" : partnerName.Substring(0, 1).ToUpper();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MeetupChatPage] Fallback Load Error: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetTabBarIsVisible(this, false);
        StartRealTimeListeners();
        await ClearUnreadBadge();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // We delay the tab bar visibility change so it happens AFTER the exit animation
        Task.Delay(300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => {
            Shell.SetTabBarIsVisible(this, true);
        }));

        Task.Run(() => {
            _chatListener?.Remove();
            _offerStatusListener?.Remove();
        });
    }

    private void OnBackgroundTapped(object sender, TappedEventArgs e) => MessageInput.Unfocus();

    protected override bool OnBackButtonPressed()
    {
        if (FullscreenImageOverlay.IsVisible)
        {
            FullscreenImageOverlay.IsVisible = false;
            FullscreenImageView.Source = null;
            return true;
        }
        return base.OnBackButtonPressed();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        MessageInput.Unfocus();
        await Task.Delay(50);
        await Navigation.PopAsync();
    }

    private async Task ClearUnreadBadge()
    {
        if (string.IsNullOrEmpty(_currentUserId) || string.IsNullOrEmpty(_offerId)) return;
        try
        {
            var updates = new Dictionary<string, object> { { $"unreadCounts.{_currentUserId}", 0 } };
            await CrossCloudFirestore.Current.Instance.Collection("offers").Document(_offerId).UpdateAsync(updates);
        }
        catch { }
    }

    private DateTime GetMytTime(DateTime utcTime)
    {
        try
        {
            TimeZoneInfo mytZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, mytZone);
        }
        catch
        {
            return utcTime.AddHours(8);
        }
    }

    private void StartRealTimeListeners()
    {
        var db = CrossCloudFirestore.Current.Instance;

        _offerStatusListener = db.Collection("offers").Document(_offerId)
            .AddSnapshotListener((snapshot, error) =>
            {
                if (error != null || snapshot == null || !snapshot.Exists) return;

                var offer = snapshot.ToObject<Offer>();
                if (offer == null) return;

                _partnerId = (_currentUserId == offer.BuyerId) ? offer.SellerId : offer.BuyerId;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Color primaryColor = Application.Current.Resources.TryGetValue("Primary", out var p) ? (Color)p : Color.FromArgb("#0056B3");

                    if (offer.Status == "Accepted")
                    {
                        Step2Box.Color = primaryColor;
                        DealStatusLabel.Text = "Escrow Locked";
                        DealStatusLabel.TextColor = primaryColor;
                    }
                    else if (offer.Status == "Completed")
                    {
                        Step2Box.Color = primaryColor;
                        Step3Box.Color = primaryColor;
                        DealStatusLabel.Text = "Handover Complete";
                        DealStatusLabel.TextColor = Color.FromArgb("#198754");
                    }
                    else if (offer.Status == "Disputed")
                    {
                        DealStatusLabel.Text = "Escrow Frozen";
                        DealStatusLabel.TextColor = Color.FromArgb("#DC3545");
                    }
                    else if (offer.Status == "Cancelled")
                    {
                        DealStatusLabel.Text = "[SYSTEM] Deal Cancelled";
                        DealStatusLabel.TextColor = Color.FromArgb("#DC3545");
                        Step2Box.Color = Color.FromArgb("#E2E3E5");
                        Step3Box.Color = Color.FromArgb("#E2E3E5");
                    }

                    if (offer.UnreadCounts != null && !string.IsNullOrEmpty(_partnerId) && offer.UnreadCounts.ContainsKey(_partnerId))
                    {
                        IsPartnerRead = offer.UnreadCounts[_partnerId] == 0;
                    }
                    else
                    {
                        IsPartnerRead = false;
                    }
                });
            });

        _chatListener = db.Collection("messages")
            .WhereEqualsTo("offerId", _offerId)
            .AddSnapshotListener((snapshot, error) =>
            {
                if (error != null || snapshot == null) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var incomingDocs = snapshot.Documents
                        .Select(d => d.ToObject<Message>())
                        .OrderBy(m => m.Timestamp);

                    foreach (var msg in incomingDocs)
                    {
                        if (!ChatMessages.Any(m => m.Id == msg.Id))
                        {
                            msg.IsMine = msg.SenderId == _currentUserId;

                            DateTime mytTime = GetMytTime(msg.Timestamp);
                            msg.FormattedMytTime = mytTime.ToString("hh:mm tt");

                            var lastMsg = ChatMessages.LastOrDefault();
                            DateTime localCurrentDate = GetMytTime(DateTime.UtcNow).Date;

                            if (lastMsg == null || GetMytTime(lastMsg.Timestamp).Date != mytTime.Date)
                            {
                                msg.ShowDateHeader = true;
                                if (mytTime.Date == localCurrentDate)
                                    msg.DateHeaderText = "Today";
                                else if (mytTime.Date == localCurrentDate.AddDays(-1))
                                    msg.DateHeaderText = "Yesterday";
                                else
                                    msg.DateHeaderText = mytTime.ToString("MMM dd, yyyy");
                            }
                            else
                            {
                                msg.ShowDateHeader = false;
                            }

                            ChatMessages.Add(msg);
                        }
                    }

                    if (ChatMessages.Count > 0)
                        ChatMessagesList.ScrollTo(ChatMessages.Last(), position: ScrollToPosition.End, animate: false);

                    ClearUnreadBadge();
                });
            });
    }

    private async void OnSendTextClicked(object sender, EventArgs e)
    {
        string text = MessageInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        MessageInput.Text = string.Empty;
        await DispatchMessageAsync(text, null, "text");
    }

    private async void OnGalleryClicked(object sender, EventArgs e)
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();
        if (photo != null) await ProcessAndSendMediaAsync(photo);
    }

    private async Task ProcessAndSendMediaAsync(FileResult photo)
    {
        UploadOverlay.IsVisible = true;
        try
        {
            Account account = new Account(AppSettings.CloudinaryCloudName, AppSettings.CloudinaryApiKey, AppSettings.CloudinaryApiSecret);
            Cloudinary cloudinary = new Cloudinary(account);
            Stream finalUploadStream = null;

#if ANDROID
            try
            {
                var exifInterface = new Android.Media.ExifInterface(photo.FullPath);
                int orientation = exifInterface.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, (int)Android.Media.Orientation.Normal);
                int rotationAngle = 0;

                if (orientation == (int)Android.Media.Orientation.Rotate90) rotationAngle = 90;
                else if (orientation == (int)Android.Media.Orientation.Rotate180) rotationAngle = 180;
                else if (orientation == (int)Android.Media.Orientation.Rotate270) rotationAngle = 270;

                Android.Graphics.Bitmap bitmap = Android.Graphics.BitmapFactory.DecodeFile(photo.FullPath);

                if (bitmap.Width > 1500 || bitmap.Height > 1500)
                {
                    float scale = Math.Min(1500f / bitmap.Width, 1500f / bitmap.Height);
                    bitmap = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, (int)(bitmap.Width * scale), (int)(bitmap.Height * scale), true);
                }

                if (rotationAngle != 0)
                {
                    Android.Graphics.Matrix matrix = new Android.Graphics.Matrix();
                    matrix.PostRotate(rotationAngle);
                    bitmap = Android.Graphics.Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);
                }

                var memoryStream = new System.IO.MemoryStream();
                bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 80, memoryStream);
                memoryStream.Position = 0;
                finalUploadStream = memoryStream;
            }
            catch (Exception)
            {
                finalUploadStream = await photo.OpenReadAsync();
            }
#else
            finalUploadStream = await photo.OpenReadAsync();
#endif

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(photo.FileName, finalUploadStream),
                Folder = "unimart_chats",
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);

            await DispatchMessageAsync("", uploadResult.SecureUrl.ToString(), "image");
            if (finalUploadStream != null) finalUploadStream.Dispose();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Upload Failed", ex.Message, "OK");
        }
        finally
        {
            UploadOverlay.IsVisible = false;
        }
    }

    private async Task DispatchMessageAsync(string text, string mediaUrl, string type)
    {
        try
        {
            var db = CrossCloudFirestore.Current.Instance;
            var msgRef = db.Collection("messages").Document();
            var offerRef = db.Collection("offers").Document(_offerId);

            var msg = new Message
            {
                Id = msgRef.Id,
                OfferId = _offerId,
                SenderId = _currentUserId,
                Text = text,
                MediaUrl = mediaUrl,
                MessageType = type,
                Timestamp = DateTime.UtcNow
            };

            string dashboardOverrideText = type == "image" ? "[Sent an image]" : text;

            await db.RunTransactionAsync(transaction =>
            {
                var offerSnap = transaction.Get(offerRef);
                transaction.Set(msgRef, msg);

                // 🛡️ THE LAZY INIT: If this is the very first message, create the Offer right now!
                if (!offerSnap.Exists)
                {
                    var newOffer = new Offer
                    {
                        Id = _offerId,
                        ListingId = _listingId,
                        BuyerId = _buyerId,
                        SellerId = _sellerId,
                        BuyerName = _buyerName,
                        SellerName = _sellerName,
                        UnreadCounts = new Dictionary<string, int> { { _sellerId, 1 }, { _buyerId, 0 } },
                        Participants = new List<string> { _buyerId, _sellerId },
                        ListingTitlePreview = _listingTitle,
                        ListingImagePreview = _listingImage,
                        OfferPrice = _offerPrice,
                        LastMessageText = dashboardOverrideText,
                        LastMessageTime = DateTime.UtcNow,
                        Status = "Inquiry",
                        Timestamp = DateTime.UtcNow
                    };
                    transaction.Set(offerRef, newOffer);
                }
                else
                {
                    // Existing chat: Just update the latest message
                    transaction.Update(offerRef, new Dictionary<string, object>
                    {
                        { "lastMessageText", dashboardOverrideText },
                        { "lastMessageTime", DateTime.UtcNow }
                    });

                    if (!string.IsNullOrEmpty(_partnerId))
                    {
                        transaction.Update(offerRef, $"unreadCounts.{_partnerId}", FieldValue.Increment(1));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DispatchMessage] Error: {ex.Message}");
        }
    }

    public void OnImageTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string imageUrl && !string.IsNullOrEmpty(imageUrl))
        {
            FullscreenImageView.Source = imageUrl;
            FullscreenImageOverlay.IsVisible = true;
            MessageInput.Unfocus();
        }
    }

    public void OnCloseFullscreenClicked(object sender, EventArgs e)
    {
        FullscreenImageOverlay.IsVisible = false;
        FullscreenImageView.Source = null;
    }
}