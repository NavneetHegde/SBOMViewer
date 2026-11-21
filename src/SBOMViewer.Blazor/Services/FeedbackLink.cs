using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Builds a pre-filled "new issue" URL for the project's GitHub repository.
///
/// There is no login and no backend, so GitHub is the feedback channel: it costs no infrastructure,
/// stores nothing here, and the audience already knows how to write a good bug report. Pre-filling
/// the version and format removes the most common round-trip on any report.
///
/// <para>
/// Everything this puts in the body is visible to the user in GitHub's issue form before they
/// submit, and editable there — which is the point. It carries the app version, the SBOM
/// <em>format</em> and the browser string, and deliberately <b>never</b> the file name or any
/// document content: issues are public, and an SBOM file name alone can name an employer's
/// unreleased product.
/// </para>
/// </summary>
public static class FeedbackLink
{
    private const string NewIssueUrl = "https://github.com/NavneetHegde/SBOMViewer/issues/new";

    /// <summary>Long user-agent strings are pure noise past this point, and URLs have limits.</summary>
    private const int MaxUserAgentLength = 200;

    /// <param name="appVersion">Value shown in the footer, e.g. "3.2.5".</param>
    /// <param name="sbomFormat">Display label such as "CycloneDX 1.6", or null when nothing is loaded.</param>
    /// <param name="userAgent">navigator.userAgent, or null when the interop call did not land.</param>
    public static string Build(string? appVersion, string? sbomFormat = null, string? userAgent = null) =>
        Compose("Feedback: ",
            [
                "### What happened, or what would you like to see?",
                "",
                "",
                "### Steps to reproduce (if it's a bug)",
                "",
                "1. ",
            ],
            appVersion, sbomFormat, userAgent);

    /// <summary>
    /// "Report a wrong result" for one Quality check. Carries the profile, check id, reference and the
    /// counts — enough to find the rule that misfired — and, like <see cref="Build"/>, never the file
    /// name or any component names: an internal package name can be as revealing as a file name.
    /// </summary>
    public static string ForQualityCheck(
        string? appVersion, string? sbomFormat, string? userAgent, QualityReport report, QualityCheck check)
    {
        var result = check.Status switch
        {
            CheckStatus.NotExpressible => "not expressible in this format",
            _ when check.Scope == CheckScope.Component => $"{check.Status} — {check.Satisfied} of {check.Total} components populated",
            CheckStatus.Pass => "Pass — present",
            CheckStatus.Partial => "Partial — declared unknown",
            _ => "Fail — missing",
        };

        return Compose($"Quality check: {check.Title} ({report.ProfileLabel})",
            [
                $"- Check: {check.Title} (`{check.Id}`, {report.ProfileLabel})",
                $"- Element: {check.Reference}",
                $"- Result shown: {result}",
                "",
                "### What looks wrong, and what did you expect?",
                "",
                "",
                "### Which tool generated the SBOM? (name and version, e.g. syft 1.18.0)",
                "",
                "",
            ],
            appVersion, sbomFormat, userAgent);
    }

    private static string Compose(string title, IEnumerable<string> sections, string? appVersion, string? sbomFormat, string? userAgent)
    {
        var env = new List<string> { $"- SBOM Viewer: v{Blank(appVersion, "unknown")}" };

        if (!string.IsNullOrWhiteSpace(sbomFormat))
            env.Add($"- SBOM format: {sbomFormat}");

        if (!string.IsNullOrWhiteSpace(userAgent))
            env.Add($"- Browser: {Truncate(userAgent.Trim(), MaxUserAgentLength)}");

        var body = string.Join("\n", [
            "<!-- Thanks for taking the time. Everything below is pre-filled — edit or delete freely. -->",
            "",
            .. sections,
            "",
            "---",
            "",
            "_Environment (pre-filled):_",
            .. env,
            "",
            "_No SBOM contents or file names are included — add a sample yourself only if you're happy to make it public._",
        ]);

        return $"{NewIssueUrl}?labels=feedback"
             + $"&title={Uri.EscapeDataString(title)}"
             + $"&body={Uri.EscapeDataString(body)}";
    }

    private static string Blank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
