using System;
using System.Collections.Generic;
using System.Linq;
using Android.Animation;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using AuthenticatorPro.Data;
using AuthenticatorPro.Data.Source;
using AuthenticatorPro.Shared.Data;
using AuthenticatorPro.Shared.Data.Generator;
using Object = Java.Lang.Object;

namespace AuthenticatorPro.List
{
    internal sealed class AuthenticatorListAdapter : RecyclerView.Adapter, IReorderableListAdapter
    {
        private const int MaxCodeGroupSize = 4;
        private const int MaxProgress = 10000;

        public event EventHandler<int> ItemClick;
        public event EventHandler<int> MenuClick;

        public event EventHandler MovementStarted;
        public event EventHandler MovementFinished;

        private readonly ViewMode _viewMode;
        private readonly bool _isDark;
        
        private readonly AuthenticatorSource _authSource;
        private readonly CustomIconSource _customIconSource;
       
        // Cache the remaining seconds per period, a relative DateTime calculation can be expensive
        // Cache the remaining progress per period, to keep all progressbars in sync
        private readonly Dictionary<int, int> _remainingSecondsPerPeriod;
        private readonly Dictionary<int, int> _remainingProgressPerPeriod;

        public enum ViewMode
        {
            Default = 0, Compact = 1, Tile = 2
        }

        public AuthenticatorListAdapter(AuthenticatorSource authSource, CustomIconSource customIconSource, ViewMode viewMode, bool isDark)
        {
            _authSource = authSource;
            _customIconSource = customIconSource;
            _viewMode = viewMode;
            _isDark = isDark;

            _remainingSecondsPerPeriod = new Dictionary<int, int>();
            _remainingProgressPerPeriod = new Dictionary<int, int>();
        }

        public override int ItemCount => _authSource.GetView().Count;

        public void MoveItemView(int oldPosition, int newPosition)
        {
            _authSource.Swap(oldPosition, newPosition);
            NotifyItemMoved(oldPosition, newPosition);
        }

        public async void NotifyMovementFinished(int oldPosition, int newPosition)
        {
            MovementFinished?.Invoke(this, null);
            await _authSource.CommitRanking();
        }

        public void NotifyMovementStarted()
        {
            MovementStarted?.Invoke(this, null);
        }

        public override long GetItemId(int position)
        {
            return _authSource.Get(position).Secret.GetHashCode();
        }

        public override async void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position)
        {
            var auth = _authSource.Get(position);

            if(auth == null)
                return;

            var holder = (AuthenticatorListHolder) viewHolder;

            holder.Issuer.Text = auth.Issuer;
            holder.Username.Text = auth.Username;

            holder.Username.Visibility = String.IsNullOrEmpty(auth.Username)
                ? ViewStates.Gone
                : ViewStates.Visible;

            holder.Code.Text = PadCode(auth.GetCode(), auth.Digits);

            if(auth.Icon != null && auth.Icon.StartsWith(CustomIcon.Prefix))
            {
                var id = auth.Icon.Substring(1);
                var customIcon = _customIconSource.Get(id);
                
                if(customIcon != null)
                    holder.Icon.SetImageBitmap(await customIcon.GetBitmap()); 
                else
                    holder.Icon.SetImageResource(Icon.GetService(Icon.Default, _isDark));
            }
            else
                holder.Icon.SetImageResource(Icon.GetService(auth.Icon, _isDark));
                
            switch(auth.Type.GetGenerationMethod())
            {
                case GenerationMethod.Time:
                    holder.RefreshButton.Visibility = ViewStates.Gone;
                    holder.ProgressBar.Visibility = ViewStates.Visible;
                    AnimateProgressBar(holder.ProgressBar, auth.Period);
                    break;

                case GenerationMethod.Counter:
                    holder.RefreshButton.Visibility = auth.TimeRenew < DateTime.UtcNow
                        ? ViewStates.Visible
                        : ViewStates.Gone;

                    holder.ProgressBar.Visibility = ViewStates.Invisible;
                    break;
            }
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position, IList<Object> payloads)
        {
            if(payloads == null || payloads.Count == 0)
            {
                OnBindViewHolder(viewHolder, position);
                return;
            }

            var auth = _authSource.Get(position);
            var holder = (AuthenticatorListHolder) viewHolder;

            switch(auth.Type.GetGenerationMethod())
            {
                case GenerationMethod.Time:
                    holder.Code.Text = PadCode(auth.GetCode(), auth.Digits);
                    AnimateProgressBar(holder.ProgressBar, auth.Period);
                    break;
                
                case GenerationMethod.Counter:
                    if(auth.TimeRenew < DateTime.UtcNow)
                        holder.RefreshButton.Visibility = ViewStates.Visible;
                    break;
            } 
        }

        public void Tick(bool invalidateCache = false)
        {
            if(invalidateCache)
            {
                _remainingSecondsPerPeriod.Clear();
                _remainingProgressPerPeriod.Clear();
            }
            
            foreach(var period in _remainingSecondsPerPeriod.Keys.ToList())
                _remainingSecondsPerPeriod[period]--;
            
            for(var i = 0; i < _authSource.GetView().Count; ++i)
            {
                var auth = _authSource.Get(i);

                if(auth.Type.GetGenerationMethod() != GenerationMethod.Time || _remainingSecondsPerPeriod.GetValueOrDefault(auth.Period, -1) > 0)
                    continue;

                NotifyItemChanged(i, true);
            }

            foreach(var period in _remainingSecondsPerPeriod.Keys.ToList())
            {
                if(_remainingSecondsPerPeriod[period] < 0)
                    _remainingSecondsPerPeriod[period] = period;
            }

            _remainingProgressPerPeriod.Clear();
        }

        private int GetRemainingProgress(int period, int remainingSeconds)
        {
            var remainingProgress = _remainingProgressPerPeriod.GetValueOrDefault(period, -1);

            if(remainingProgress > -1)
                return remainingProgress;

            remainingProgress = (int) Math.Floor((double) MaxProgress * remainingSeconds / period);
            _remainingProgressPerPeriod.Add(period, remainingProgress);
            return remainingProgress;
        }

        private int GetRemainingSeconds(int period)
        {
            var remainingSeconds = _remainingSecondsPerPeriod.GetValueOrDefault(period, -1);

            if(remainingSeconds > -1)
                return remainingSeconds;

            remainingSeconds = period - (int) DateTimeOffset.Now.ToUnixTimeSeconds() % period;
            _remainingSecondsPerPeriod.Add(period, remainingSeconds);
            return remainingSeconds;
        }

        private void AnimateProgressBar(ProgressBar progressBar, int period)
        {
            var remainingSeconds = GetRemainingSeconds(period);
            var remainingProgress = GetRemainingProgress(period, remainingSeconds);
            progressBar.Progress = remainingProgress;
            
            var animator = ObjectAnimator.OfInt(progressBar, "progress", 0);
            animator.SetDuration(remainingSeconds * 1000);
            animator.SetInterpolator(new LinearInterpolator());
            animator.Start();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var layout = _viewMode switch
            {
                ViewMode.Compact => Resource.Layout.listItemAuthCompact,
                ViewMode.Tile => Resource.Layout.listItemAuthTile,
                _ => Resource.Layout.listItemAuth
            };
            
            var itemView = LayoutInflater.From(parent.Context).Inflate(layout, parent, false);

            var holder = new AuthenticatorListHolder(itemView);
            holder.Click += ItemClick;
            holder.MenuClick += MenuClick;
            holder.RefreshClick += OnRefreshClick;

            return holder;
        }

        private async void OnRefreshClick(object sender, int position)
        {
            await _authSource.IncrementCounter(position);
            NotifyItemChanged(position);
        }

        private static string PadCode(string code, int digits)
        {
            code ??= "".PadRight(digits, '-');

            var spacesInserted = 0;
            var groupSize = Math.Min(MaxCodeGroupSize, digits / 2);

            for(var i = 0; i < digits; ++i)
            {
                if(i % groupSize == 0 && i > 0)
                {
                    code = code.Insert(i + spacesInserted, " ");
                    spacesInserted++;
                }
            }

            return code;
        }
    }
}