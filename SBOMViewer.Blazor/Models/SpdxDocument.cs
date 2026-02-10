using System.Text.Json.Serialization;

namespace SBOMViewer.Blazor.Models.Spdx;

public class SpdxDocument
{
    [JsonPropertyName("spdxVersion")]
    public string SpdxVersion { get; set; } = "";

    [JsonPropertyName("dataLicense")]
    public string DataLicense { get; set; } = "";

    [JsonPropertyName("SPDXID")]
    public string SpdxId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("documentNamespace")]
    public string DocumentNamespace { get; set; } = "";

    [JsonPropertyName("creationInfo")]
    public CreationInfo CreationInfo { get; set; } = new();

    [JsonPropertyName("files")]
    public List<SpdxFile> Files { get; set; } = new();

    [JsonPropertyName("packages")]
    public List<SpdxPackage> Packages { get; set; } = new();

    [JsonPropertyName("relationships")]
    public List<SpdxRelationship> Relationships { get; set; } = new();
}

public class CreationInfo
{
    [JsonPropertyName("created")]
    public string Created { get; set; } = "";

    [JsonPropertyName("creators")]
    public List<string> Creators { get; set; } = new();
}

public class SpdxFile
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("SPDXID")]
    public string SpdxId { get; set; } = "";

    [JsonPropertyName("licenseConcluded")]
    public string LicenseConcluded { get; set; } = "";

    [JsonPropertyName("copyrightText")]
    public string CopyrightText { get; set; } = "";

    [JsonPropertyName("checksums")]
    public List<Checksum> Checksums { get; set; } = new();
}

public class Checksum
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "";

    [JsonPropertyName("checksumValue")]
    public string ChecksumValue { get; set; } = "";
}

public class SpdxPackage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("SPDXID")]
    public string SpdxId { get; set; } = "";

    [JsonPropertyName("versionInfo")]
    public string VersionInfo { get; set; } = "";

    [JsonPropertyName("supplier")]
    public string Supplier { get; set; } = "";

    [JsonPropertyName("downloadLocation")]
    public string DownloadLocation { get; set; } = "";

    [JsonPropertyName("licenseConcluded")]
    public string LicenseConcluded { get; set; } = "";

    [JsonPropertyName("licenseDeclared")]
    public string LicenseDeclared { get; set; } = "";

    [JsonPropertyName("copyrightText")]
    public string CopyrightText { get; set; } = "";

    [JsonPropertyName("filesAnalyzed")]
    public bool? FilesAnalyzed { get; set; }

    [JsonPropertyName("externalRefs")]
    public List<ExternalReference> ExternalReferences { get; set; } = new();
}

public class ExternalReference
{
    [JsonPropertyName("referenceCategory")]
    public string? ReferenceCategory { get; set; }

    [JsonPropertyName("referenceType")]
    public string? ReferenceType { get; set; }

    [JsonPropertyName("referenceLocator")]
    public string? ReferenceLocator { get; set; }
}

public class SpdxRelationship
{
    [JsonPropertyName("relationshipType")]
    public string RelationshipType { get; set; } = "";

    [JsonPropertyName("relatedSpdxElement")]
    public string RelatedSpdxElement { get; set; } = "";

    [JsonPropertyName("spdxElementId")]
    public string SpdxElementId { get; set; } = "";
}

