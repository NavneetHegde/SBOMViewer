using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public class SbomState
{
    public event Action? OnChange;

    private JsonDocument? _document;
    public JsonDocument? Document
    {
        get => _document;
        set
        {
            _document?.Dispose();
            _document = value;
            NotifyStateChanged();
        }
    }

    public SchemaNode? Schema { get; set; }
    public SbomFormat? DetectedFormat { get; set; }
    public string? FileName { get; set; }

    /// <summary>
    /// True while the bundled demo document is open, so the viewer can label it and nobody
    /// mistakes demo data for their own. Assigned by <see cref="Load"/>.
    /// </summary>
    public bool IsSample { get; private set; }

    /// <summary>
    /// The one way to open a document. Every field is assigned on every call — so a real file
    /// opened after the demo cannot inherit its label — and <see cref="Document"/> goes last,
    /// because assigning it is what notifies subscribers.
    /// </summary>
    public void Load(SbomLoadResult result, bool isSample = false)
    {
        if (!result.Success) return;

        Schema         = result.Schema;
        DetectedFormat = result.Format;
        FileName       = result.FileName;
        IsSample       = isSample;
        Document       = result.Document;
    }

    public void Clear()
    {
        _document?.Dispose();
        _document = null;
        Schema = null;
        DetectedFormat = null;
        FileName = null;
        IsSample = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
