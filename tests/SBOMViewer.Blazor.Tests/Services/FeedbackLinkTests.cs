using System.Web;
using FluentAssertions;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class FeedbackLinkTests
{
    /// <summary>Decodes the body back out of the URL, so tests assert on what GitHub will show.</summary>
    private static string BodyOf(string url) =>
        HttpUtility.ParseQueryString(new Uri(url).Query)["body"] ?? "";

    [Fact]
    public void Build_ProducesAValidAbsoluteGitHubUrl()
    {
        var url = FeedbackLink.Build("3.2.5", "CycloneDX 1.6", "Mozilla/5.0");

        var uri = new Uri(url);
        uri.Host.Should().Be("github.com");
        uri.AbsolutePath.Should().Be("/NavneetHegde/SBOMViewer/issues/new");
        HttpUtility.ParseQueryString(uri.Query)["labels"].Should().Be("feedback");
    }

    [Fact]
    public void Build_IncludesVersionFormatAndBrowser()
    {
        var body = BodyOf(FeedbackLink.Build("3.2.5", "SPDX 3.0.1", "Mozilla/5.0 (Windows NT 10.0)"));

        body.Should().Contain("v3.2.5");
        body.Should().Contain("SPDX 3.0.1");
        body.Should().Contain("Mozilla/5.0 (Windows NT 10.0)");
    }

    [Fact]
    public void Build_OmitsFormatLineWhenNoDocumentIsOpen()
    {
        var body = BodyOf(FeedbackLink.Build("3.2.5"));

        body.Should().Contain("v3.2.5");
        body.Should().NotContain("SBOM format:", "an empty label would read as a rendering bug");
        body.Should().NotContain("Browser:");
    }

    [Fact]
    public void Build_SurvivesAMissingVersion()
    {
        BodyOf(FeedbackLink.Build(null)).Should().Contain("vunknown");
    }

    [Theory]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) & more")]
    [InlineData("weird/agent?with=query#and-fragment")]
    public void Build_EncodesCharactersThatWouldOtherwiseTruncateTheUrl(string ua)
    {
        // '&', '?' and '#' unescaped would silently cut the body short at GitHub's end — the exact
        // failure that looks fine locally and produces half an issue template in production.
        var url = FeedbackLink.Build("3.2.5", "CycloneDX 1.6", ua);

        BodyOf(url).Should().Contain(ua);
        HttpUtility.ParseQueryString(new Uri(url).Query)["labels"].Should().Be("feedback");
    }

    [Fact]
    public void Build_TruncatesAnAbsurdlyLongUserAgent()
    {
        var body = BodyOf(FeedbackLink.Build("3.2.5", null, new string('x', 5000)));

        body.Should().Contain("…");
        body.Length.Should().BeLessThan(1500, "the URL has to stay well inside browser limits");
    }

    [Fact]
    public void Build_NeverLeaksDocumentContent()
    {
        // Issues are public. Only the version, the format label and the browser may travel — a file
        // name alone can name an employer's unreleased product.
        var url = FeedbackLink.Build("3.2.5", "CycloneDX 1.6", "Mozilla/5.0");

        url.Should().NotContainAny("acme", ".json", "pkg:", "CVE-");
    }

    [Fact]
    public void Build_TellsTheUserNothingFromTheirSbomWasIncluded()
    {
        BodyOf(FeedbackLink.Build("3.2.5", "CycloneDX 1.6"))
            .Should().Contain("No SBOM contents or file names are included");
    }
}
