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

namespace UniMart.Views
{
    public partial class AddListingPage : ContentPage
    {
        public ObservableCollection<PhotoWrapper> PreviewImages { get; set; } = new();

        private Listing _existingItem;
        private bool _isEditMode => _existingItem != null;

        public AddListingPage(Listing itemToEdit = null)
        {
            InitializeComponent();
            BindingContext = this;
            _existingItem = itemToEdit;

            if (_isEditMode)
            {
                PrepareEditMode();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetTabBarIsVisible(this, false);
            UpdatePhotoUI();
        }

        private void PrepareEditMode()
        {
            PageTitleLabel.Text = "Edit Listing";
            PostButton.Text = "Save Changes";

            TitleEntry.Text = _existingItem.Title;
            DescriptionEditor.Text = _existingItem.Description;
            PriceEntry.Text = _existingItem.Price.ToString("0.00");
            CategoryPicker.SelectedItem = _existingItem.Category;
            ConditionPicker.SelectedItem = _existingItem.Condition;

            if (_existingItem.ImageUrls != null)
            {
                foreach (var url in _existingItem.ImageUrls)
                {
                    PreviewImages.Add(new PhotoWrapper
                    {
                        IsExisting = true,
                        Url = url,
                        Image = ImageSource.FromUri(new Uri(url))
                    });
                }
            }
            UpdatePhotoUI();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            UnfocusAll();

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else
            {
                await Shell.Current.GoToAsync("///HomePage");
            }
        }

        private async void OnInputFocused(object sender, FocusEventArgs e)
        {
            if (e.IsFocused && sender is Element focusedElement)
            {
                if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 260;
                await Task.Delay(300);
                if (this.FindByName<ScrollView>("FormScrollView") is ScrollView scrollView)
                {
                    await scrollView.ScrollToAsync(focusedElement, ScrollToPosition.Center, true);
                }
            }
        }

        private async void OnInputUnfocused(object sender, FocusEventArgs e)
        {
            await Task.Delay(100);
            if (!TitleEntry.IsFocused && !DescriptionEditor.IsFocused && !PriceEntry.IsFocused)
            {
                if (FormScrollView != null) await FormScrollView.ScrollToAsync(0, 0, true);
                if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 0;
            }
        }

        private void OnPriceCompleted(object sender, EventArgs e)
        {
            PriceEntry.Unfocus();
        }

        private async void UnfocusAll()
        {
            TitleEntry.Unfocus();
            DescriptionEditor.Unfocus();
            PriceEntry.Unfocus();

            await Task.Delay(50);
            if (FormScrollView != null) await FormScrollView.ScrollToAsync(0, 0, true);
            if (KeyboardSpacer != null) KeyboardSpacer.HeightRequest = 0;
        }

        private void OnBackgroundTapped(object sender, TappedEventArgs e)
        {
            UnfocusAll();
        }

        protected override bool OnBackButtonPressed()
        {
            if (TitleEntry.IsFocused || DescriptionEditor.IsFocused || PriceEntry.IsFocused)
            {
                UnfocusAll();
                return true;
            }
            return base.OnBackButtonPressed();
        }

        private void OnTitleChanged(object sender, TextChangedEventArgs e)
        {
            int length = e.NewTextValue?.Length ?? 0;
            TitleCountLabel.Text = $"{length}/50";
            TitleCountLabel.TextColor = length >= 45 ? Color.FromArgb("#DC3545") : Application.Current.Resources.TryGetValue("Gray500", out var g) ? (Color)g : Colors.Gray;
        }

        private void OnDescriptionChanged(object sender, TextChangedEventArgs e)
        {
            int length = e.NewTextValue?.Length ?? 0;
            DescriptionCountLabel.Text = $"{length}/500";
            DescriptionCountLabel.TextColor = length >= 450 ? Color.FromArgb("#DC3545") : Application.Current.Resources.TryGetValue("Gray500", out var g) ? (Color)g : Colors.Gray;
        }

        private async void OnSelectPhotoClicked(object sender, EventArgs e)
        {
            if (PreviewImages.Count >= 5)
            {
                await DisplayAlert("Limit Reached", "You can only upload up to 5 photos.", "OK");
                return;
            }

            try
            {
                var pickOptions = new PickOptions { PickerTitle = "Select Photos", FileTypes = FilePickerFileType.Images };
                var results = await FilePicker.Default.PickMultipleAsync(pickOptions);

                if (results != null && results.Any())
                {
                    foreach (var photo in results.Take(5 - PreviewImages.Count))
                    {
                        var stream = await photo.OpenReadAsync();
                        PreviewImages.Add(new PhotoWrapper
                        {
                            IsExisting = false,
                            File = photo,
                            Image = ImageSource.FromStream(() => stream)
                        });
                    }
                    UpdatePhotoUI();
                }
            }
            catch (Exception)
            {
                await DisplayAlert("Error", "Could not access system gallery.", "OK");
            }
        }

        private void OnDeletePhotoClicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            if (btn.CommandParameter is PhotoWrapper targetPhoto)
            {
                PreviewImages.Remove(targetPhoto);
                UpdatePhotoUI();
            }
        }

        private void UpdatePhotoUI()
        {
            PhotoCountLabel.Text = $"({PreviewImages.Count}/5)";

            if (PreviewImages.Count == 0)
            {
                EmptyDropzone.IsVisible = true;
                PhotoPreviewsScroll.IsVisible = false;
            }
            else
            {
                EmptyDropzone.IsVisible = false;
                PhotoPreviewsScroll.IsVisible = true;
                SmallAddPhotoButton.IsVisible = PreviewImages.Count < 5;
            }
        }

        private async void OnPostClicked(object sender, EventArgs e)
        {
            UnfocusAll(); // Ensure keyboard drops before processing

            string title = TitleEntry.Text?.Trim();
            string description = DescriptionEditor.Text?.Trim();
            string category = CategoryPicker.SelectedItem?.ToString();
            string condition = ConditionPicker.SelectedItem?.ToString();
            string priceText = PriceEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) ||
                string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(condition) ||
                string.IsNullOrWhiteSpace(priceText) || PreviewImages.Count == 0)
            {
                await DisplayAlert("Missing Details", "Please fill in all fields and provide at least one photo.", "OK");
                return;
            }

            if (!double.TryParse(priceText, out double price) || price <= 0)
            {
                await DisplayAlert("Invalid Price", "Please enter a valid numeric price.", "OK");
                return;
            }

            // 🔥 HIDE THE FORM AND SHOW THE FULL-SCREEN LOADING OVERLAY
            MainContentGrid.IsVisible = false;
            StatusOverlay.IsVisible = true;
            LoadingStateContainer.IsVisible = true;
            SuccessStateContainer.IsVisible = false;

            try
            {
                LoadingStatusLabel.Text = _isEditMode ? "Updating Listing..." : "Publishing Listing...";

                List<string> finalImageUrls = PreviewImages.Where(p => p.IsExisting).Select(p => p.Url).ToList();
                var newPhotosToUpload = PreviewImages.Where(p => !p.IsExisting).Select(p => p.File).ToList();

                if (newPhotosToUpload.Count > 0)
                {
                    Account account = new Account(AppSettings.CloudinaryCloudName, AppSettings.CloudinaryApiKey, AppSettings.CloudinaryApiSecret);
                    Cloudinary cloudinary = new Cloudinary(account);

                    var uploadTasks = newPhotosToUpload.Select(async photo =>
                    {
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
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Compression Failed: {ex.Message}");
                            finalUploadStream = await photo.OpenReadAsync();
                        }
#else
                        finalUploadStream = await photo.OpenReadAsync();
#endif

                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(photo.FileName, finalUploadStream),
                            Folder = "unimart_items",
                            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);
                        if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);

                        if (finalUploadStream != null) finalUploadStream.Dispose();
                        return uploadResult.SecureUrl.ToString();
                    });

                    string[] newlyUploadedUrls = await Task.WhenAll(uploadTasks);
                    finalImageUrls.AddRange(newlyUploadedUrls);
                }

                var db = CrossCloudFirestore.Current.Instance;

                if (_isEditMode)
                {
                    await db.Collection("listings").Document(_existingItem.Id).UpdateAsync(new
                    {
                        title = title,
                        description = description,
                        category = category,
                        condition = condition,
                        price = Math.Round(price, 2),
                        imageUrls = finalImageUrls,
                        updatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;
                    var newDoc = db.Collection("listings").Document();

                    var newListing = new Listing
                    {
                        Id = newDoc.Id,
                        SellerId = currentUser.Uid,
                        Title = title,
                        Description = description,
                        Category = category,
                        Condition = condition,
                        Price = Math.Round(price, 2),
                        Status = "Available",
                        ImageUrls = finalImageUrls,
                        CreatedAt = DateTime.UtcNow
                    };

                    await newDoc.SetAsync(newListing);
                }

                // 🔥 TRANSITION FROM "LOADING" TO "SUCCESS" CHECKMARK
                LoadingStateContainer.IsVisible = false;
                SuccessStateContainer.IsVisible = true;

                // Wait 1.5 seconds so the user can see the success message
                await Task.Delay(1500);

                // Navigate away (The page is destroyed gracefully in the background)
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopAsync();
                }
                else
                {
                    await Shell.Current.GoToAsync("///HomePage");
                }
            }
            catch (Exception ex)
            {
                // If it fails, bring the form back so they can fix it
                StatusOverlay.IsVisible = false;
                MainContentGrid.IsVisible = true;
                await DisplayAlert("Action Failed", ex.Message, "OK");
            }
        }
    }

    public class PhotoWrapper
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ImageSource Image { get; set; }
        public bool IsExisting { get; set; }
        public string Url { get; set; }
        public FileResult File { get; set; }
    }
}