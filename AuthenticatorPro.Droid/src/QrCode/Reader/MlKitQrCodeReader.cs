// Copyright (C) 2023 jmh
// SPDX-License-Identifier: GPL-3.0-only

#if !FDROID

using Android.Content;
using Android.Gms.Extensions;
using Android.Runtime;
using Java.Lang;
using Serilog;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Google.MLKit.Common;
using Xamarin.Google.MLKit.Vision.BarCode;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.Common;
using Exception = System.Exception;
using Uri = Android.Net.Uri;

namespace AuthenticatorPro.Droid.QrCode.Reader
{
    public class MlKitQrCodeReader : IQrCodeReader
    {
        private readonly ILogger _log = Log.ForContext<MlKitQrCodeReader>();

        public async Task<string> ScanImageFromFileAsync(Context context, Uri uri)
        {
            try
            {
                MlKit.Initialize(context);
            }
            catch (IllegalStateException e)
            {
                _log.Warning(e, "MlKit already initialised");
            }
            
            InputImage image;

            try
            {
                image = await Task.Run(() => InputImage.FromFilePath(context, uri));
            }
            catch (Exception e)
            {
                throw new IOException("Failed to read file", e);
            }

            var options = new BarcodeScannerOptions.Builder()
                .SetBarcodeFormats(Barcode.FormatQrCode)
                .Build();

            var scanner = BarcodeScanning.GetClient(options);
            var barcodes = await scanner.Process(image).AsAsync<JavaList<Barcode>>();

            return barcodes
                .Select(b => b.RawValue)
                .FirstOrDefault();
        }
    }
}

#endif