using Plugin.CloudFirestore;

namespace UniMart
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void OnTestConnectionClicked(object sender, EventArgs e)
        {
            try
            {
                // 1. Create a simple data object
                var testData = new
                {
                    project = "UniMart FYP",
                    status = "Connection Successful!",
                    timestamp = FieldValue.ServerTimestamp // Uses Google's server time, not local phone time
                };

                // 2. Send it to a temporary collection called "system_tests"
                await CrossCloudFirestore.Current
                    .Instance
                    .Collection("system_tests")
                    .Document("init_test")
                    .SetAsync(testData);

                // 3. Show a success popup in the app
                await DisplayAlert("Success", "Data sent to Firebase! Go check your console.", "Awesome");
            }
            catch (Exception ex)
            {
                // If it fails, this will tell us exactly why
                await DisplayAlert("Connection Failed", $"Error: {ex.Message}", "OK");
            }
        }
    }
}
