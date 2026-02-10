namespace SBOMViewer.Blazor.Models;

public class SchemaNode
{
    /// <summary>JSON property name (e.g., "bomFormat", "components").</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Human-readable title from schema (e.g., "BOM Format").</summary>
    public string? Title { get; set; }

    /// <summary>Description from schema.</summary>
    public string? Description { get; set; }

    /// <summary>The JSON type: string, integer, boolean, number, array, object.</summary>
    public SchemaNodeType NodeType { get; set; }

    /// <summary>For arrays, the schema of the array items.</summary>
    public SchemaNode? ItemSchema { get; set; }

    /// <summary>For objects, the child property schemas keyed by property name.</summary>
    public Dictionary<string, SchemaNode> Properties { get; set; } = new();

    /// <summary>Ordered list of property names for display ordering.</summary>
    public List<string> PropertyOrder { get; set; } = new();

    /// <summary>For enums, the allowed values.</summary>
    public List<string>? EnumValues { get; set; }

    /// <summary>Whether the schema was marked as deprecated.</summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Rendering hint: controls how this node should be rendered.</summary>
    public RenderHint Hint { get; set; } = RenderHint.Auto;
}

public enum SchemaNodeType
{
    String,
    Integer,
    Number,
    Boolean,
    Array,
    Object,
    Unknown
}

public enum RenderHint
{
    Auto,
    AccordionSection,
    KeyValueGroup,
    SearchableList,
    BadgeList
}
