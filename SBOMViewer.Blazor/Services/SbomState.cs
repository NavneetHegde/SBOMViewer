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

    public void Clear()
    {
        _document?.Dispose();
        _document = null;
        Schema = null;
        DetectedFormat = null;
        FileName = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
