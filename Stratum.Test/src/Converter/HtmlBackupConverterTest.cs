// Copyright (C) 2025 jmh
// SPDX-License-Identifier: GPL-3.0-only

using System.Linq;
using System.Threading.Tasks;
using Stratum.Core;
using Stratum.Core.Converter;
using Moq;
using Stratum.Test.Converter.Fixture;
using Xunit;

namespace Stratum.Test.Converter
{
    public class HtmlBackupConverterTest : IClassFixture<HtmlBackupFixture>
    {
        private readonly HtmlBackupFixture _htmlBackupFixture;
        private readonly HtmlBackupConverter _htmlBackupConverter;

        public HtmlBackupConverterTest(HtmlBackupFixture htmlBackupFixture)
        {
            _htmlBackupFixture = htmlBackupFixture;

            var iconResolver = new Mock<IIconResolver>();
            iconResolver.Setup(r => r.FindServiceKeyByName(It.IsAny<string>())).Returns("icon");

            _htmlBackupConverter = new HtmlBackupConverter(iconResolver.Object);
        }

        [Fact]
        public async Task ConvertAsync()
        {
            var result = await _htmlBackupConverter.ConvertAsync(_htmlBackupFixture.Data);

            Assert.Empty(result.Failures);

            Assert.Equal(7, result.Backup.Authenticators.Count());
            Assert.Null(result.Backup.Categories);
            Assert.Null(result.Backup.AuthenticatorCategories);
            Assert.Null(result.Backup.CustomIcons);
        }
    }
}