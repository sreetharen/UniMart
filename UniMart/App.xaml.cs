using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Plugin.CloudFirestore;
using Plugin.FirebaseAuth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UniMart.Views;

namespace UniMart
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private bool _isOfflinePageShowing = false;
        private NoInternetPage _offlinePage;

        public App()
        {
            InitializeComponent();

            // 🔥 THE BLACK BOX: Catch Native & Background Crashes
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
                File.WriteAllText(path, $"CRITICAL CRASH:\n{e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
                File.WriteAllText(path, $"BACKGROUND CRASH:\n{e.Exception}");
            };

            // 🔥 EXPERT FIX: Isolated preprocessor lines using 'this' instead of the uninitialized Application.Current
#if ANDROID
            this.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
                .UseWindowSoftInputModeAdjust(Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif

            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
#if ANDROID                
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
#if ANDROID                
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
            });

            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
#if ANDROID                
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
            });

            // 🧼 CLEAN & MINIMAL: White background with your primary blue spinner while Firebase loads
            this.MainPage = new ContentPage
            {
                BackgroundColor = Microsoft.Maui.Graphics.Colors.White,
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Microsoft.Maui.Graphics.Color.FromArgb("#0056B3"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HeightRequest = 40,
                    WidthRequest = 40
                }
            };

            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

            CheckForCrashLogs();
        }

        private void CheckForCrashLogs()
        {
            string path = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
            if (File.Exists(path))
            {
                string crashData = File.ReadAllText(path);

                Microsoft.Maui.Controls.Application.Current.Dispatcher.Dispatch(async () =>
                {
                    await Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert("Silent Crash Found!", crashData, "Clear Log & Continue");
                    File.Delete(path);
                });
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                ShowOfflinePage();
                return;
            }

            _ = CheckUserSecurityStatusAsync();
        }

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess != NetworkAccess.Internet)
            {
                ShowOfflinePage();
            }
            else
            {
                HideOfflinePage();

                if (this.MainPage is ContentPage && !(this.MainPage is LoginPage) && !(this.MainPage is AppShell))
                {
                    _ = CheckUserSecurityStatusAsync();
                }
            }
        }

        private void ShowOfflinePage()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!_isOfflinePageShowing && this.MainPage != null)
                {
                    _offlinePage = new NoInternetPage();
                    await this.MainPage.Navigation.PushModalAsync(_offlinePage);
                    _isOfflinePageShowing = true;
                }
            });
        }

        private void HideOfflinePage()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_isOfflinePageShowing && this.MainPage != null)
                {
                    await this.MainPage.Navigation.PopModalAsync();
                    _isOfflinePageShowing = false;
                }
            });
        }

        private async Task CheckUserSecurityStatusAsync()
        {
            var currentUser = CrossFirebaseAuth.Current.Instance.CurrentUser;

            if (currentUser != null)
            {
                try
                {
                    var db = CrossCloudFirestore.Current.Instance;
                    var userDoc = await db.Collection("users").Document(currentUser.Uid).GetAsync();

                    if (userDoc.Exists)
                    {
                        var userData = userDoc.ToObject<Dictionary<string, object>>();

                        string accountStatus = userData != null && userData.ContainsKey("accountStatus")
                                               ? userData["accountStatus"].ToString()
                                               : "Unknown";

                        if (accountStatus != "Active")
                        {
                            CrossFirebaseAuth.Current.Instance.SignOut();

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                this.MainPage = new LoginPage();
                            });
                            return;
                        }
                    }
                    else
                    {
                        CrossFirebaseAuth.Current.Instance.SignOut();
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            this.MainPage = new LoginPage();
                        });
                        return;
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        this.MainPage = new AppShell();
                    });
                }
                catch (Exception)
                {
                    CrossFirebaseAuth.Current.Instance.SignOut();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        this.MainPage = new LoginPage();
                    });
                }
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    this.MainPage = new LoginPage();
                });
            }
        }
    }
}