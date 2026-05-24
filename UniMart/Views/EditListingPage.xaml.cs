using Plugin.CloudFirestore;
using UniMart.Models;

namespace UniMart.Views;

public partial class EditListingPage : ContentPage
{
    private Listing _itemToEdit;

    public EditListingPage(Listing item)
    {
        InitializeComponent();
        _itemToEdit = item;

        // Pre-fill the form with existing data
        TitleEntry.Text = _itemToEdit.Title;
        DescriptionEditor.Text = _itemToEdit.Description;
        PriceEntry.Text = _itemToEdit.Price.ToString();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnUpdateClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleEntry.Text) || !double.TryParse(PriceEntry.Text, out double newPrice))
        {
            await DisplayAlert("Error", "Please provide valid details.", "OK");
            return;
        }

        try
        {
            await CrossCloudFirestore.Current.Instance
                .Collection("listings").Document(_itemToEdit.Id)
                .UpdateAsync(new
                {
                    title = TitleEntry.Text.Trim(),
                    description = DescriptionEditor.Text.Trim(),
                    price = Math.Round(newPrice, 2)
                });

            await DisplayAlert("Success", "Listing updated!", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }
}