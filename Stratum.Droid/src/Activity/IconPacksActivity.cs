// Copyright (C) 2023 jmh
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidX.Core.Graphics;
using AndroidX.RecyclerView.Widget;
using Stratum.Core.Entity;
using Stratum.Core.Service;
using Stratum.Droid.Shared.Util;
using Google.Android.Material.Button;
using Google.Android.Material.Dialog;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;
using ProtoBuf;
using Serilog;
using Stratum.Droid.Interface;
using Stratum.Droid.Interface.Adapter;
using Stratum.Droid.Interface.LayoutManager;
using Stratum.Droid.Persistence.View;
using Stratum.Droid.Util;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

namespace Stratum.Droid.Activity
{
    [Activity]
    public class IconPacksActivity : SensitiveSubActivity
    {
        private const int RequestAdd = 0;

        private readonly ILogger _log = Log.ForContext<IconPacksActivity>();
        private readonly IIconPackView _iconPackView;
        private readonly IIconPackService _iconPackService;

        private LinearLayout _emptyStateLayout;
        private FloatingActionButton _addButton;
        private IconPackListAdapter _iconPackListAdapter;
        private RecyclerView _packList;

        public IconPacksActivity() : base(Resource.Layout.activityIconPacks)
        {
            _iconPackView = Dependencies.Resolve<IIconPackView>();
            _iconPackService = Dependencies.Resolve<IIconPackService>();
        }

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            SupportActionBar.SetTitle(Resource.String.iconPacks);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetDisplayShowHomeEnabled(true);
            SupportActionBar.SetHomeAsUpIndicator(Resource.Drawable.baseline_arrow_back_24);

            _addButton = FindViewById<FloatingActionButton>(Resource.Id.buttonAdd);
            _addButton.Click += delegate { StartFilePickActivity("*/*", RequestAdd); };

            var downloadButton = FindViewById<MaterialButton>(Resource.Id.buttonDownloadPacks);
            downloadButton.Click += OnDownloadButtonClick;

            _iconPackListAdapter = new IconPackListAdapter(_iconPackView);
            _iconPackListAdapter.HasStableIds = true;
            _iconPackListAdapter.DeleteClicked += OnDeleteClicked;
            _iconPackListAdapter.ViewSourceClicked += OnViewSourceClicked;

            _packList = FindViewById<RecyclerView>(Resource.Id.list);
            _emptyStateLayout = FindViewById<LinearLayout>(Resource.Id.layoutEmptyState);

            _packList.SetAdapter(_iconPackListAdapter);
            _packList.HasFixedSize = true;

            var layout = new FixedGridLayoutManager(this, 1);
            _packList.SetLayoutManager(layout);
            _packList.AddItemDecoration(new GridSpacingItemDecoration(this, layout, 12, true));

            var layoutAnimation = AnimationUtils.LoadLayoutAnimation(this, Resource.Animation.layout_animation_fade_in);
            _packList.LayoutAnimation = layoutAnimation;

            await _iconPackView.LoadFromPersistenceAsync();

            RunOnUiThread(delegate
            {
                CheckEmptyState();

                _iconPackListAdapter.NotifyDataSetChanged();
                _packList.ScheduleLayoutAnimation();
            });
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            if (requestCode != RequestAdd || resultCode != Result.Ok)
            {
                return;
            }

            SetLoading(true);
            MemoryStream stream = null;
            IconPack pack;

            try
            {
                var data = await FileUtil.ReadFileAsync(this, intent.Data);
                stream = new MemoryStream(data);
                pack = await Task.Run(() => Serializer.Deserialize<IconPack>(stream));
            }
            catch (Exception e)
            {
                ShowSnackbar(e is ProtoException
                    ? Resource.String.invalidIconPackError
                    : Resource.String.filePickError, Snackbar.LengthShort);

                _log.Error(e, "Failed to read icon pack");
                SetLoading(false);
                return;
            }
            finally
            {
                stream?.Close();
            }

            try
            {
                await _iconPackService.ImportPackAsync(pack);
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to import icon pack");
                ShowSnackbar(Resource.String.genericError, Snackbar.LengthShort);
                SetLoading(false);
                return;
            }

            var message = string.Format(GetString(Resource.String.importIconPackSuccess), pack.Icons.Count);
            ShowSnackbar(message, Snackbar.LengthLong);

            await _iconPackView.LoadFromPersistenceAsync();
            SetLoading(false);

            RunOnUiThread(delegate
            {
                CheckEmptyState();
                _iconPackListAdapter.NotifyDataSetChanged();
            });
        }

        private void CheckEmptyState()
        {
            if (!_iconPackView.Any())
            {
                if (_packList.Visibility == ViewStates.Visible)
                {
                    AnimUtil.FadeOutView(_packList, AnimUtil.LengthShort);
                }

                if (_emptyStateLayout.Visibility == ViewStates.Invisible)
                {
                    AnimUtil.FadeInView(_emptyStateLayout, AnimUtil.LengthLong);
                }
            }
            else
            {
                if (_packList.Visibility == ViewStates.Invisible)
                {
                    AnimUtil.FadeInView(_packList, AnimUtil.LengthLong);
                }

                if (_emptyStateLayout.Visibility == ViewStates.Visible)
                {
                    AnimUtil.FadeOutView(_emptyStateLayout, AnimUtil.LengthShort);
                }
            }
        }

        private void OnDownloadButtonClick(object sender, EventArgs e)
        {
            var url = GetString(Resource.String.website) + "/icon-packs";
            StartWebBrowserActivity(url);
        }

        private void OnDeleteClicked(object sender, IconPack pack)
        {
            var builder = new MaterialAlertDialogBuilder(this);
            builder.SetMessage(Resource.String.confirmIconPackDelete);
            builder.SetTitle(Resource.String.delete);
            builder.SetIcon(Resource.Drawable.baseline_delete_24);
            builder.SetCancelable(true);

            builder.SetPositiveButton(Resource.String.delete, async delegate
            {
                try
                {
                    await _iconPackService.DeletePackAsync(pack);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failed to delete icon pack");
                    ShowSnackbar(Resource.String.genericError, Snackbar.LengthShort);
                }

                var position = _iconPackView.IndexOf(pack.Name);
                await _iconPackView.LoadFromPersistenceAsync();

                RunOnUiThread(delegate
                {
                    _iconPackListAdapter.NotifyItemRemoved(position);
                    CheckEmptyState();
                });
            });

            builder.SetNegativeButton(Resource.String.cancel, delegate { });

            var dialog = builder.Create();
            dialog.Show();
        }

        private void OnViewSourceClicked(object sender, IconPack pack)
        {
            StartWebBrowserActivity(pack.Url);
        }

        public override bool OnSupportNavigateUp()
        {
            Finish();
            return base.OnSupportNavigateUp();
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home)
            {
                Finish();
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnApplySystemBarInsets(Insets insets)
        {
            base.OnApplySystemBarInsets(insets);

            var bottomPadding = DimenUtil.DpToPx(this, ListFabPaddingBottom) + insets.Bottom;
            _packList.SetPadding(0, 0, 0, bottomPadding);

            var layoutParams = (ViewGroup.MarginLayoutParams) AddButton.LayoutParameters;
            layoutParams.SetMargins(layoutParams.LeftMargin, layoutParams.TopMargin, layoutParams.RightMargin,
                layoutParams.BottomMargin + insets.Bottom);
        }
    }
}