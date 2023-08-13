// Copyright (C) 2022 jmh
// SPDX-License-Identifier: GPL-3.0-only

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AuthenticatorPro.Droid.Util;
using Google.Android.Material.AppBar;
using Google.Android.Material.Color;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Environment = System.Environment;
using Path = System.IO.Path;
using Uri = Android.Net.Uri;

namespace AuthenticatorPro.Droid.Activity
{
    [Activity]
    internal class AboutActivity : BaseActivity
    {
        private const int RequestSaveLog = 0;

        private readonly ILogger _log = Log.ForContext<AboutActivity>();

        public AboutActivity() : base(Resource.Layout.activityAbout) { }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var toolbar = FindViewById<MaterialToolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetTitle(Resource.String.about);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetDisplayShowHomeEnabled(true);
            SupportActionBar.SetHomeAsUpIndicator(Resource.Drawable.baseline_arrow_back_24);

            string version;

            try
            {
#if FDROID
                var versionName = PackageUtil.GetVersionName(PackageManager, PackageName);
                version = $"{versionName} F-Droid";
#else
                version = PackageUtil.GetVersionName(PackageManager, PackageName);
#endif
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to get current version");
                version = "unknown";
            }

            var surface = MaterialColors.GetColor(this, Resource.Attribute.colorSurface, 0);
            var onSurface = MaterialColors.GetColor(this, Resource.Attribute.colorOnSurface, 0);
            var primary = MaterialColors.GetColor(this, Resource.Attribute.colorPrimary, 0);

            var icon = await AssetUtil.ReadAllBytes(Assets, "icon.png");

#if FDROID
            const string extraLicenseFile = "license.extra.fdroid.html";
#else
            const string extraLicenseFile = "license.extra.html";
#endif

            var extraLicense = await AssetUtil.ReadAllTextAsync(Assets, extraLicenseFile);

            var html = (await AssetUtil.ReadAllTextAsync(Assets, "about.html"))
                .Replace("%ICON", $"data:image/png;base64,{Convert.ToBase64String(icon)}")
                .Replace("%VERSION", version)
                .Replace("%LICENSE", extraLicense)
                .Replace("%SURFACE", ColourToHexString(surface))
                .Replace("%ON_SURFACE", ColourToHexString(onSurface))
                .Replace("%PRIMARY", ColourToHexString(primary));

            var webView = FindViewById<WebView>(Resource.Id.webView);
            webView.LoadDataWithBaseURL("file:///android_asset", html, "text/html", "utf-8", null);
        }

        public override bool OnSupportNavigateUp()
        {
            Finish();
            return base.OnSupportNavigateUp();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.about, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Android.Resource.Id.Home:
                    Finish();
                    return true;

                case Resource.Id.actionSaveLog:
                    StartLogSaveActivity();
                    return true;

                default:
                    return base.OnOptionsItemSelected(item);
            }
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            if (requestCode != RequestSaveLog || resultCode != Result.Ok)
            {
                return;
            }

            try
            {
                await SaveLogToFileAsync(intent.Data);
            }
            catch (Exception e)
            {
                _log.Error(e, "Log saving failed");
                Toast.MakeText(this, Resource.String.genericError, ToastLength.Short).Show();
                return;
            }

            Toast.MakeText(this, Resource.String.saveSuccess, ToastLength.Short).Show();
        }

        private void StartLogSaveActivity()
        {
            var path = GetLogPath();

            if (path == null)
            {
                Toast.MakeText(this, Resource.String.noLogFile, ToastLength.Short).Show();
                return;
            }

            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("text/plain");
            intent.PutExtra(Intent.ExtraTitle, Path.GetFileName(path));

            BaseApplication.PreventNextAutoLock = true;

            try
            {
                StartActivityForResult(intent, RequestSaveLog);
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, Resource.String.filePickerMissing, ToastLength.Long).Show();
            }
        }

        private static string GetLogPath()
        {
            var privateDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var file = Directory.GetFiles(privateDir, "*.log").FirstOrDefault();
            return file == null ? null : Path.Combine(privateDir, file);
        }

        private async Task SaveLogToFileAsync(Uri uri)
        {
            var contents = await File.ReadAllTextAsync(GetLogPath());
            await FileUtil.WriteFile(this, uri, contents);
        }

        private static string ColourToHexString(int colour)
        {
            var parsed = new Color(colour);
            return "#" + parsed.R.ToString("X2") + parsed.G.ToString("X2") + parsed.B.ToString("X2");
        }
    }
}