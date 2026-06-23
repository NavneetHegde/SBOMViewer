using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class LicenseClassifierTests
{
    [Theory]
    [InlineData("MIT")]
    [InlineData("Apache-2.0")]
    [InlineData("BSD-3-Clause")]
    [InlineData("ISC")]
    [InlineData("0BSD")]
    [InlineData("Unlicense")]
    [InlineData("PostgreSQL")]
    [InlineData("Zlib")]
    public void Classify_PermissiveLicenses_ReturnsPermissive(string license)
    {
        LicenseClassifier.Classify(license).Should().Be(LicenseRisk.Permissive);
    }

    [Theory]
    [InlineData("LGPL-2.1")]
    [InlineData("LGPL-3.0")]
    [InlineData("MPL-2.0")]
    [InlineData("EPL-2.0")]
    [InlineData("CDDL-1.1")]
    public void Classify_WeakCopyleftLicenses_ReturnsWeakCopyleft(string license)
    {
        LicenseClassifier.Classify(license).Should().Be(LicenseRisk.WeakCopyleft);
    }

    [Theory]
    [InlineData("GPL-2.0")]
    [InlineData("GPL-3.0")]
    [InlineData("AGPL-3.0")]
    [InlineData("SSPL-1.0")]
    public void Classify_StrongCopyleftLicenses_ReturnsStrongCopyleft(string license)
    {
        LicenseClassifier.Classify(license).Should().Be(LicenseRisk.StrongCopyleft);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOASSERTION")]
    [InlineData("PostgreSQL License")]
    [InlineData("Custom-Internal-License")]
    public void Classify_UnknownOrEmptyLicenses_ReturnsUnknown(string? license)
    {
        LicenseClassifier.Classify(license).Should().Be(LicenseRisk.Unknown);
    }

    [Theory]
    [InlineData("mit")]
    [InlineData("Mit")]
    [InlineData("APACHE-2.0")]
    public void Classify_IsCaseInsensitive(string license)
    {
        LicenseClassifier.Classify(license).Should().NotBe(LicenseRisk.Unknown);
    }

    [Theory]
    [InlineData("GPL-2.0-only", LicenseRisk.StrongCopyleft)]
    [InlineData("GPL-3.0-or-later", LicenseRisk.StrongCopyleft)]
    [InlineData("LGPL-2.1-only", LicenseRisk.WeakCopyleft)]
    [InlineData("GPL-2.0+", LicenseRisk.StrongCopyleft)]
    public void Classify_StripsSpdxSuffixesBeforeMatching(string license, LicenseRisk expected)
    {
        LicenseClassifier.Classify(license).Should().Be(expected);
    }
}
