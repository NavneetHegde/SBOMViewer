using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public class SchemaService
{
    /// <summary>
    /// Builds a SchemaNode tree directly from the uploaded JSON document.
    /// No external schema files needed — infers types from the actual data.
    /// </summary>
    public SchemaNode BuildFromJson(JsonElement root)
    {
        var node = BuildNode(root, "root", 0);
        ApplyRenderHints(node);
        return node;
    }

    private static SchemaNode BuildNode(JsonElement element, string propertyName, int depth)
    {
        var node = new SchemaNode
        {
            PropertyName = propertyName,
            Title = HumanizePropertyName(propertyName)
        };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.NodeType = SchemaNodeType.Object;
                foreach (var prop in element.EnumerateObject())
                {
                    // Only recurse one level deep for top-level properties to keep it fast
                    // Deeper rendering is handled by the components themselves
                    var childNode = depth < 1
                        ? BuildNode(prop.Value, prop.Name, depth + 1)
                        : new SchemaNode
                        {
                            PropertyName = prop.Name,
                            Title = HumanizePropertyName(prop.Name),
                            NodeType = MapValueKind(prop.Value.ValueKind)
                        };
                    node.Properties[prop.Name] = childNode;
                    node.PropertyOrder.Add(prop.Name);
                }
                break;

            case JsonValueKind.Array:
                node.NodeType = SchemaNodeType.Array;
                // Peek at first item to build item schema
                if (element.GetArrayLength() > 0)
                {
                    var first = element[0];
                    if (first.ValueKind == JsonValueKind.Object)
                    {
                        node.ItemSchema = BuildNode(first, "item", depth + 1);
                    }
                }
                break;

            case JsonValueKind.String:
                node.NodeType = SchemaNodeType.String;
                break;
            case JsonValueKind.Number:
                node.NodeType = SchemaNodeType.Number;
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                node.NodeType = SchemaNodeType.Boolean;
                break;
            default:
                node.NodeType = SchemaNodeType.Unknown;
                break;
        }

        return node;
    }

    private static SchemaNodeType MapValueKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => SchemaNodeType.String,
        JsonValueKind.Number => SchemaNodeType.Number,
        JsonValueKind.True or JsonValueKind.False => SchemaNodeType.Boolean,
        JsonValueKind.Array => SchemaNodeType.Array,
        JsonValueKind.Object => SchemaNodeType.Object,
        _ => SchemaNodeType.Unknown
    };

    private static void ApplyRenderHints(SchemaNode root)
    {
        foreach (var prop in root.Properties.Values)
        {
            if (prop.NodeType is SchemaNodeType.String or SchemaNodeType.Number
                or SchemaNodeType.Boolean)
            {
                prop.Hint = RenderHint.KeyValueGroup;
            }
        }

        // Searchable list hints for well-known large array sections
        string[] searchable = ["components", "packages", "files", "vulnerabilities", "services"];
        foreach (var name in searchable)
        {
            if (root.Properties.TryGetValue(name, out var n))
                n.Hint = RenderHint.SearchableList;
        }

        string[] accordion = ["dependencies", "relationships"];
        foreach (var name in accordion)
        {
            if (root.Properties.TryGetValue(name, out var n))
                n.Hint = RenderHint.AccordionSection;
        }
    }

    private static string HumanizePropertyName(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "root" || name == "item")
            return name;
        var result = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        result = result.Replace("-", " ").Replace("_", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result);
    }
}
