﻿using System;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using AndroidX.Preference;
using Uri = Android.Net.Uri;

namespace AuthenticatorPro.Preferences
{
    internal class RatePreference : Preference
    {
        public RatePreference(Context context) : base(context)
        {
        }

        public RatePreference(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public RatePreference(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs,
            defStyleAttr)
        {
        }

        public RatePreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base(context,
            attrs, defStyleAttr, defStyleRes)
        {
        }

        protected RatePreference(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnClick()
        {
            var intent = new Intent(Intent.ActionView, Uri.Parse("market://details?id=me.jmh.authenticatorpro"));

            try
            {
                Context.StartActivity(intent);
            }
            catch(ActivityNotFoundException)
            {
                Toast.MakeText(Context, Resource.String.googlePlayNotInstalledError, ToastLength.Short).Show();
            }
        }
    }
}