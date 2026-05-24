using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Firebase;

namespace UniMart
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize
                               | ConfigChanges.Orientation
                               | ConfigChanges.UiMode
                               | ConfigChanges.ScreenLayout
                               | ConfigChanges.SmallestScreenSize
                               | ConfigChanges.Density,

        // Makes the page resize properly when keyboard appears
        WindowSoftInputMode = SoftInput.AdjustResize
    )]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Prevent Firebase reinitialization crash on app resume
            if (FirebaseApp.Instance == null)
            {
                var options = new FirebaseOptions.Builder()
                    .SetProjectId("unimart-97506")
                    .SetApplicationId("1:190230731837:android:28eacf1f208bcc29219e54")
                    .SetApiKey("AIzaSyBsymYwvDXPb4Z6sy7QBLHzDEZ7YCH-uxE")
                    .Build();

                FirebaseApp.InitializeApp(this, options);
            }

            // Modern Android keyboard/layout handling
            Window?.SetSoftInputMode(SoftInput.AdjustResize);

            // Optional status bar styling
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                Window.DecorView.SystemUiVisibility =
                    (StatusBarVisibility)SystemUiFlags.LightStatusBar;

                Window.SetStatusBarColor(Android.Graphics.Color.White);
            }
        }
    }
}