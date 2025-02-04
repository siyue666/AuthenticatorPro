// Copyright (C) 2025 jmh
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Stratum.Core.Backup;

namespace Stratum.Core.Converter
{
    public class HtmlBackupConverter : UriListBackupConverter
    {
        public HtmlBackupConverter(IIconResolver iconResolver) : base(iconResolver)
        {
        }

        public override Task<ConversionResult> ConvertAsync(byte[] data, string password = null)
        {
            var html = Encoding.UTF8.GetString(data);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            
            var builder = new StringBuilder();
            var nodes = document.DocumentNode.SelectNodes("//code");

            foreach (var node in nodes)
            {
                builder.AppendLine(node.InnerText);
            }

            return Task.FromResult(ConvertText(builder.ToString()));
        }
    }
}