using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

/// <summary>
/// The parts of the recents store that do not need a browser: the content identity and the
/// display label. IndexedDB behaviour is covered end to end by RecentSbomTests.
/// </summary>
public class RecentSbomStoreTests
{
    [Fact]
    public void ContentHash_IsLowerHexSha256OfUtf8()
    {
        // FIPS 180-2 test vector.
        RecentSbomStore.ContentHash("abc")
            .Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void ContentHash_DiffersForAnyChangeInContent()
    {
        RecentSbomStore.ContentHash("{\"a\":1}")
            .Should().NotBe(RecentSbomStore.ContentHash("{\"a\":1} "));
    }

    [Fact]
    public void ContentHash_EncodesNonAsciiAsUtf8()
    {
        // "é" is two UTF-8 bytes; hashing UTF-16 code units would give a different digest.
        RecentSbomStore.ContentHash("é")
            .Should().Be(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(new byte[] { 0xC3, 0xA9 })));
    }

    [Theory]
    [InlineData(null, "bom.json")]
    [InlineData("", "bom.json")]
    [InlineData("storefront 2.4.1", "bom.json · storefront 2.4.1")]
    public void DisplayName_AddsSubjectWhenPresent(string? subject, string expected)
    {
        new RecentSbom(1, "bom.json", "CycloneDX 1.6", 10, 0, subject).DisplayName.Should().Be(expected);
    }

    [Fact]
    public void HasContent_DefaultsToTrue_ForRowsSavedBeforeTheField()
    {
        var row = System.Text.Json.JsonSerializer.Deserialize<RecentSbom>(
            """{ "id": 1, "name": "bom.json", "format": "CycloneDX 1.6", "size": 10, "savedAt": 0 }""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        row!.HasContent.Should().BeTrue();
        row.Subject.Should().BeNull();
    }
}
