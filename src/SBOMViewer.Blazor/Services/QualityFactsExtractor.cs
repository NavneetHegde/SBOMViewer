using System.Text.Json;
using System.Text.RegularExpressions;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Reads the fields the quality scorecard needs, per format, into a format-neutral
/// <see cref="QualityFacts"/>. Deliberately separate from <see cref="ComponentRowExtractor"/>: that one
/// collapses <c>NOASSERTION</c> to "" for the license table, which would lose the declared-unknown
/// distinction the scorecard reports.
///
/// Component scope matches the rest of the app — top-level CycloneDX <c>components</c>, SPDX 2.x
/// <c>packages</c>, SPDX 3.0.1 <c>software_Package</c> elements — so the counts agree with the
/// Components tab.
/// </summary>
public static partial class QualityFactsExtractor
{
    public static QualityFacts Extract(JsonElement root, SbomFormat? format)
    {
        if (root.ValueKind != JsonValueKind.Object) return new QualityFacts();

        return format switch
        {
            SbomFormat.CycloneDX_1_5 or SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7 => ExtractCycloneDx(root),
            SbomFormat.SPDX_2_2 or SbomFormat.SPDX_2_3 => ExtractSpdx2(root),
            SbomFormat.SPDX_3_0 => ExtractSpdx3(root),
            _ => new QualityFacts()
        };
    }

    // ── CycloneDX 1.5 – 1.7 ─────────────────────────────────────────────────────

    private static QualityFacts ExtractCycloneDx(JsonElement root)
    {
        var meta = Obj(root, "metadata");

        // metadata.authors is "the person(s) who created the BOM". metadata.supplier and
        // metadata.manufacturer describe the *component* the BOM is about, which CISA 2026 is explicit is
        // a different entity from the SBOM author, so they do not count here.
        var author = Best(Items(meta, "authors").Select(a => Best(FromString(Str(a, "name")), FromString(Str(a, "email")))));

        var tools = CycloneDxTools(meta).ToList();
        var toolName = Best(tools.Select(t => FromString(Str(t, "name"))));
        var toolVersion = tools.Count == 0 ? FactState.Missing : Best(tools.Select(t => FromString(Str(t, "version"))));

        var lifecycle = Best(Items(meta, "lifecycles").Select(l => Best(FromString(Str(l, "phase")), FromString(Str(l, "name")))));

        var sbomVersion = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number
            ? FactState.Present
            : FromString(Str(root, "serialNumber"));

        // Every bom-ref named on either side of a dependency entry. An entry with an empty dependsOn still
        // places its ref in the graph — it asserts "this component has no dependencies".
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var anyEdge = false;
        foreach (var dep in Items(root, "dependencies"))
        {
            if (Str(dep, "ref") is { Length: > 0 } r) covered.Add(r);
            foreach (var key in new[] { "dependsOn", "provides" })
                foreach (var target in Items(dep, key))
                    if (target.ValueKind == JsonValueKind.String && target.GetString() is { Length: > 0 } t)
                    {
                        covered.Add(t);
                        anyEdge = true;
                    }
        }

        var components = new List<ComponentFacts>();
        var index = 0;
        foreach (var c in Items(root, "components"))
        {
            // A cryptographic asset (CBOM: an algorithm, key, certificate or protocol) is not a unit of
            // software. It has no supplier, version or hash in the CISA sense, so counting it would fail the
            // component checks merely because the SBOM also inventories its cryptography.
            // A "file" component is the CycloneDX counterpart of an SPDX File, and the SPDX extractors count
            // packages only. syft writes the same workflow files as SPDX `files` and as CycloneDX `file`
            // components; counting them here alone would score one scan differently by output format.
            if (Str(c, "type") is "cryptographic-asset" or "file") continue;
            index++;
            var name = Str(c, "name");
            var version = c.TryGetProperty("version", out var cv) ? cv.ToString() : null;

            var supplier = Best(
                Entity(Obj(c, "supplier")),
                Entity(Obj(c, "manufacturer")),          // 1.6+
                FromString(Str(c, "publisher")),
                FromString(Str(c, "author")),            // deprecated in 1.6, still common
                Best(Items(c, "authors").Select(a => FromString(Str(a, "name")))));

            var identifier = Best(
                FromString(Str(c, "purl")),
                FromString(Str(c, "cpe")),
                c.TryGetProperty("swid", out var swid) && swid.ValueKind == JsonValueKind.Object ? FactState.Present : FactState.Missing,
                NonEmptyArray(c, "omniborId"),           // 1.6+
                NonEmptyArray(c, "swhid"));              // 1.6+

            var hash = Best(Items(c, "hashes").Select(h => FromString(Str(h, "content"))));

            var license = Best(Items(c, "licenses").Select(l =>
            {
                var lic = Obj(l, "license");
                return Best(FromString(Str(lic, "id")), FromString(Str(lic, "name")), FromString(Str(l, "expression")));
            }));

            var bomRef = Str(c, "bom-ref");
            components.Add(new ComponentFacts
            {
                Label = Label(name, version, index),
                Name = FromString(name),
                Version = FromString(version),
                Supplier = supplier,
                Identifier = identifier,
                Hash = hash,
                License = license,
                InRelationship = bomRef is not null && covered.Contains(bomRef)
            });
        }

        return new QualityFacts
        {
            DataFormat = FactState.Present,
            Author = author,
            AuthorSignature = root.TryGetProperty("signature", out var sig) && sig.ValueKind == JsonValueKind.Object
                                        ? FactState.Present : FactState.Missing,
            Timestamp = FromString(Str(meta, "timestamp")),
            ToolName = toolName,
            ToolVersion = toolVersion,
            SbomVersion = sbomVersion,
            GenerationContext = lifecycle,
            DependencyRelationships = anyEdge ? FactState.Present : FactState.Missing,
            Components = components
        };
    }

    /// <summary>
    /// <c>metadata.tools</c> has two shapes: the legacy array of <c>{vendor, name, version}</c>
    /// (deprecated in 1.5, still emitted widely), and the 1.5+ object with <c>components</c> and
    /// <c>services</c> arrays.
    /// </summary>
    private static IEnumerable<JsonElement> CycloneDxTools(JsonElement meta)
    {
        if (meta.ValueKind != JsonValueKind.Object || !meta.TryGetProperty("tools", out var tools)) yield break;

        if (tools.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tools.EnumerateArray()) yield return t;
        }
        else if (tools.ValueKind == JsonValueKind.Object)
        {
            foreach (var t in Items(tools, "components")) yield return t;
            foreach (var t in Items(tools, "services")) yield return t;
        }
    }

    // ── SPDX 2.2 / 2.3 ──────────────────────────────────────────────────────────

    /// <summary>SPDX 2.x relationship types that express a dependency or inclusion between elements.</summary>
    private static readonly HashSet<string> Spdx2DependencyTypes = new(StringComparer.Ordinal)
    {
        "DEPENDS_ON", "DEPENDENCY_OF", "CONTAINS", "CONTAINED_BY",
        "BUILD_DEPENDENCY_OF", "DEV_DEPENDENCY_OF", "OPTIONAL_DEPENDENCY_OF", "PROVIDED_DEPENDENCY_OF",
        "TEST_DEPENDENCY_OF", "RUNTIME_DEPENDENCY_OF", "DEPENDENCY_MANIFEST_OF",
        "STATIC_LINK", "DYNAMIC_LINK", "HAS_PREREQUISITE", "PREREQUISITE_FOR", "PACKAGE_OF"
    };

    private static readonly HashSet<string> Spdx2IdentifierTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "purl", "cpe22Type", "cpe23Type", "swid", "gitoid", "swh"
    };

    private static QualityFacts ExtractSpdx2(JsonElement root)
    {
        var ci = Obj(root, "creationInfo");
        var creators = Items(ci, "creators")
            .Where(c => c.ValueKind == JsonValueKind.String)
            .Select(c => c.GetString() ?? "")
            .ToList();

        // Creators are "Person: …", "Organization: …" or "Tool: …". Only the first two are an author.
        var author = Best(creators
            .Where(c => c.StartsWith("Person:", StringComparison.OrdinalIgnoreCase) ||
                        c.StartsWith("Organization:", StringComparison.OrdinalIgnoreCase))
            .Select(c => FromString(StripEntityPrefix(c))));

        var tools = creators
            .Where(c => c.StartsWith("Tool:", StringComparison.OrdinalIgnoreCase))
            .Select(c => c["Tool:".Length..].Trim())
            .ToList();
        var toolName = Best(tools.Select(FromString));
        // SPDX 2.x has no tool-version field; the spec's own examples embed it as "name-version".
        var toolVersion = tools.Count == 0 ? FactState.Missing
            : tools.Any(t => SplitToolVersion(t).Version is not null) ? FactState.Present : FactState.Missing;

        var covered = new HashSet<string>(StringComparer.Ordinal);
        var anyEdge = false;
        foreach (var rel in Items(root, "relationships"))
        {
            var type = Str(rel, "relationshipType");
            if (type is null) continue;
            var isDep = Spdx2DependencyTypes.Contains(type);
            // DESCRIBES places the root package in the graph, but is not itself a dependency.
            if (!isDep && type != "DESCRIBES") continue;
            if (isDep) anyEdge = true;
            if (Str(rel, "spdxElementId") is { Length: > 0 } a) covered.Add(a);
            if (Str(rel, "relatedSpdxElement") is { Length: > 0 } b) covered.Add(b);
        }

        var components = new List<ComponentFacts>();
        var index = 0;
        foreach (var p in Items(root, "packages"))
        {
            index++;
            var name = Str(p, "name");
            var version = Str(p, "versionInfo");

            // NTIA's supplier maps to PackageSupplier; PackageOriginator is the original producer and is
            // what CISA 2026's "Component Producer" most closely describes, so either satisfies the check.
            var supplier = Best(
                FromString(StripEntityPrefix(Str(p, "supplier"))),
                FromString(StripEntityPrefix(Str(p, "originator"))));

            var identifier = Best(Items(p, "externalRefs")
                .Where(r => Str(r, "referenceType") is { } t && Spdx2IdentifierTypes.Contains(t))
                .Select(r => FromString(Str(r, "referenceLocator"))));

            var hash = Best(Items(p, "checksums").Select(h => FromString(Str(h, "checksumValue"))));

            var license = Best(
                FromLicense(Str(p, "licenseConcluded")),
                FromLicense(Str(p, "licenseDeclared")));

            var spdxId = Str(p, "SPDXID");
            components.Add(new ComponentFacts
            {
                Label = Label(name, version, index),
                Name = FromString(name),
                Version = FromString(version),
                Supplier = supplier,
                Identifier = identifier,
                Hash = hash,
                License = license,
                InRelationship = spdxId is not null && covered.Contains(spdxId)
            });
        }

        return new QualityFacts
        {
            DataFormat = FactState.Present,
            Author = author,
            AuthorSignature = FactState.NotExpressible,   // no in-document signature in SPDX 2.x
            Timestamp = FromString(Str(ci, "created")),
            ToolName = toolName,
            ToolVersion = toolVersion,
            // documentNamespace must be a new URI for every version of the document (SPDX 2.3 §6.5), so it
            // identifies the SBOM version.
            SbomVersion = FromString(Str(root, "documentNamespace")),
            GenerationContext = FactState.NotExpressible,   // SPDX 2.x has no lifecycle field
            DependencyRelationships = anyEdge ? FactState.Present : FactState.Missing,
            Components = components
        };
    }

    // ── SPDX 3.0.1 (JSON-LD @graph) ─────────────────────────────────────────────

    private static readonly HashSet<string> Spdx3DependencyTypes = new(StringComparer.Ordinal)
    {
        "contains", "dependsOn", "hasDynamicLink", "hasStaticLink", "hasPrerequisite",
        "hasProvidedDependency", "hasOptionalDependency", "hasOptionalComponent", "hasDependencyManifest"
    };

    private static readonly HashSet<string> Spdx3IdentifierTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "packageUrl", "cpe22", "cpe23", "swid", "swhid", "gitoid"
    };

    private static QualityFacts ExtractSpdx3(JsonElement root)
    {
        if (!root.TryGetProperty("@graph", out var graph) || graph.ValueKind != JsonValueKind.Array)
            return new QualityFacts { DataFormat = FactState.Present };

        var elements = graph.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList();
        var byId = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var e in elements)
            if (Id(e) is { } id) byId.TryAdd(id, e);

        static string? Type(JsonElement e) => Str(e, "type") ?? Str(e, "@type");

        // CreationInfo is usually its own graph node (referenced as a blank node), but may be inlined.
        var ci = elements.FirstOrDefault(e => Type(e) == "CreationInfo");
        if (ci.ValueKind != JsonValueKind.Object)
            ci = elements.Select(e => Obj(e, "creationInfo")).FirstOrDefault(o => o.ValueKind == JsonValueKind.Object);

        // createdBy / suppliedBy / createdUsing hold element IDs. Resolve to the element's name; an ID that
        // does not resolve still names an agent, so it counts as present.
        FactState AgentRef(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.String) return FactState.Missing;
            var id = value.GetString();
            if (string.IsNullOrWhiteSpace(id)) return FactState.Missing;
            return byId.TryGetValue(id, out var agent) && Str(agent, "name") is { } name
                ? FromString(name)
                : FactState.Present;
        }

        var author = Best(Items(ci, "createdBy").Select(AgentRef));

        var toolNames = Items(ci, "createdUsing")
            .Where(t => t.ValueKind == JsonValueKind.String)
            .Select(t => byId.TryGetValue(t.GetString() ?? "", out var tool) ? Str(tool, "name") ?? "" : t.GetString() ?? "")
            .ToList();
        var toolName = Best(toolNames.Select(FromString));
        // The SPDX 3.0.1 Tool class has no version property; accept the SPDX 2.x "name-version" convention.
        var toolVersion = toolNames.Count == 0 ? FactState.Missing
            : toolNames.Any(t => SplitToolVersion(t).Version is not null) ? FactState.Present : FactState.Missing;

        var sbomType = Best(elements
            .Where(e => Type(e) == "software_Sbom")
            .Select(e => NonEmptyArray(e, "software_sbomType")));

        // License relationships: package → license element. Collected up front so each package is one lookup.
        var licenseTargets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var distributionArtifacts = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var anyEdge = false;
        foreach (var rel in elements.Where(e => Type(e) is "Relationship" or "LifecycleScopedRelationship"))
        {
            var relType = Str(rel, "relationshipType");
            var from = Str(rel, "from");
            var to = Items(rel, "to").Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString() ?? "").ToList();
            if (rel.TryGetProperty("to", out var toSingle) && toSingle.ValueKind == JsonValueKind.String)
                to.Add(toSingle.GetString() ?? "");
            if (relType is null || from is null) continue;

            if (relType is "hasConcludedLicense" or "hasDeclaredLicense")
            {
                if (!licenseTargets.TryGetValue(from, out var list)) licenseTargets[from] = list = [];
                list.AddRange(to);
            }
            else if (relType == "hasDistributionArtifact")
            {
                if (!distributionArtifacts.TryGetValue(from, out var list)) distributionArtifacts[from] = list = [];
                list.AddRange(to);
            }
            else if (Spdx3DependencyTypes.Contains(relType))
            {
                anyEdge = true;
                covered.Add(from);
                foreach (var t in to) covered.Add(t);
            }
        }
        // Root elements are in the graph by definition, as DESCRIBES makes them in SPDX 2.x.
        foreach (var doc in elements.Where(e => Type(e) is "SpdxDocument" or "software_Sbom"))
            foreach (var r in Items(doc, "rootElement"))
                if (r.ValueKind == JsonValueKind.String && r.GetString() is { Length: > 0 } rid) covered.Add(rid);

        FactState LicenseTarget(string id)
        {
            // The NoAssertion / None individuals are referenced by IRI rather than defined in the graph.
            if (id.EndsWith("NoAssertionLicense", StringComparison.Ordinal)) return FactState.DeclaredUnknown;
            if (id.EndsWith("NoneLicense", StringComparison.Ordinal)) return FactState.Present;
            return byId.TryGetValue(id, out var lic) && Str(lic, "simplelicensing_licenseExpression") is { } expr
                ? FromLicense(expr)
                : string.IsNullOrWhiteSpace(id) ? FactState.Missing : FactState.Present;
        }

        FactState HashOf(JsonElement element) => Best(Items(element, "verifiedUsing")
            .Where(h => Type(h) == "Hash")
            .Select(h => FromString(Str(h, "hashValue"))));

        var components = new List<ComponentFacts>();
        var index = 0;
        foreach (var e in elements.Where(e => Type(e) == "software_Package"))
        {
            index++;
            var name = Str(e, "name");
            var version = Str(e, "software_packageVersion");
            var id = Id(e);

            var supplier = Best(
                e.TryGetProperty("suppliedBy", out var sb) ? AgentRef(sb) : FactState.Missing,
                Best(Items(e, "originatedBy").Select(AgentRef)));

            var identifier = Best(
                FromString(Str(e, "software_packageUrl")),
                Best(Items(e, "externalIdentifier")
                    .Where(x => Str(x, "externalIdentifierType") is { } t && Spdx3IdentifierTypes.Contains(t))
                    .Select(x => FromString(Str(x, "identifier")))));

            // A package's own verifiedUsing often carries only a PackageVerificationCode (a digest of its
            // files, not of the artifact). Converters from SPDX 2.x move the package checksum onto the file
            // linked by hasDistributionArtifact, so that file's hash is the component's hash too.
            var hash = Best(
                HashOf(e),
                id is not null && distributionArtifacts.TryGetValue(id, out var artifacts)
                    ? Best(artifacts.Select(a => byId.TryGetValue(a, out var file) ? HashOf(file) : FactState.Missing))
                    : FactState.Missing);

            // Real 3.0.1 documents link licenses by relationship; the shorthand properties are also read
            // because this app's fixtures and ComponentRowExtractor use them.
            var license = Best(
                FromLicense(Str(e, "software_concludedLicenseExpression")),
                FromLicense(Str(e, "software_declaredLicenseExpression")),
                id is not null && licenseTargets.TryGetValue(id, out var targets)
                    ? Best(targets.Select(LicenseTarget)) : FactState.Missing);

            components.Add(new ComponentFacts
            {
                Label = Label(name, version, index),
                Name = FromString(name),
                Version = FromString(version),
                Supplier = supplier,
                Identifier = identifier,
                Hash = hash,
                License = license,
                InRelationship = id is not null && covered.Contains(id)
            });
        }

        return new QualityFacts
        {
            DataFormat = FactState.Present,
            Author = author,
            AuthorSignature = FactState.NotExpressible,   // no signature class in SPDX 3.0.1
            Timestamp = FromString(Str(ci, "created")),
            ToolName = toolName,
            ToolVersion = toolVersion,
            SbomVersion = FactState.NotExpressible,   // no document-version property in 3.0.1
            GenerationContext = sbomType,
            DependencyRelationships = anyEdge ? FactState.Present : FactState.Missing,
            Components = components
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Classifies a scalar. <c>NOASSERTION</c> and a literal "unknown" are a declared unknown. Elsewhere in
    /// SPDX <c>NONE</c> means "there is no value", which for most fields here is also an unknown; for
    /// licenses it is a real statement, see <see cref="FromLicense"/>.
    /// </summary>
    internal static FactState FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FactState.Missing;
        var v = value.Trim();
        return v.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("NONE", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)
            ? FactState.DeclaredUnknown
            : FactState.Present;
    }

    /// <summary>
    /// License <c>NONE</c> asserts the component is offered under no license — a definite answer, not an
    /// unknown. <c>NOASSERTION</c> remains a declared unknown.
    /// </summary>
    internal static FactState FromLicense(string? value) =>
        value?.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase) == true ? FactState.Present : FromString(value);

    /// <summary>Present beats DeclaredUnknown beats Missing. An empty sequence is Missing.</summary>
    internal static FactState Best(IEnumerable<FactState> states)
    {
        var best = FactState.Missing;
        foreach (var s in states)
        {
            if (s == FactState.Present) return FactState.Present;
            if (s == FactState.DeclaredUnknown) best = FactState.DeclaredUnknown;
        }
        return best;
    }

    internal static FactState Best(params FactState[] states) => Best((IEnumerable<FactState>)states);

    /// <summary>
    /// Splits "syft-0.100.0" / "Microsoft.SBOMTool-2.2.5" / "cdxgen 10.2.1" into name and version. A tool
    /// string with no trailing version yields a null version.
    /// </summary>
    internal static (string Name, string? Version) SplitToolVersion(string tool)
    {
        var m = ToolVersionRegex().Match(tool.Trim());
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : (tool.Trim(), null);
    }

    [GeneratedRegex(@"^(.+?)[\s\-_/@]v?(\d[\w.\-+]*)$")]
    private static partial Regex ToolVersionRegex();

    /// <summary>Removes the SPDX 2.x "Organization: " / "Person: " prefix from a supplier or creator.</summary>
    private static string? StripEntityPrefix(string? value)
    {
        if (value is null) return null;
        var colon = value.IndexOf(':');
        if (colon <= 0) return value;
        var prefix = value[..colon].Trim();
        return prefix.Equals("Organization", StringComparison.OrdinalIgnoreCase) ||
               prefix.Equals("Person", StringComparison.OrdinalIgnoreCase)
            ? value[(colon + 1)..].Trim()
            : value;
    }

    /// <summary>A CycloneDX organizational entity: named, or at least identified by a URL.</summary>
    private static FactState Entity(JsonElement org)
    {
        if (org.ValueKind != JsonValueKind.Object) return FactState.Missing;
        var name = FromString(Str(org, "name"));
        if (name == FactState.Present) return name;
        return Items(org, "url").Any(u => u.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(u.GetString()))
            ? FactState.Present
            : name;
    }

    private static string Label(string? name, string? version, int index)
    {
        var n = string.IsNullOrWhiteSpace(name) ? $"(unnamed component #{index})" : name;
        return string.IsNullOrWhiteSpace(version) ? n : $"{n}@{version}";
    }

    private static string? Id(JsonElement e) => Str(e, "spdxId") ?? Str(e, "@id");

    private static string? Str(JsonElement obj, string prop) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static JsonElement Obj(JsonElement obj, string prop) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object
            ? v
            : default;

    private static IEnumerable<JsonElement> Items(JsonElement obj, string prop) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : [];

    private static FactState NonEmptyArray(JsonElement obj, string prop) =>
        Items(obj, prop).Any() ? FactState.Present : FactState.Missing;
}
