using SBOMViewer.Blazor.Models.CycloneDX;
using SBOMViewer.Blazor.Models.Spdx;

namespace SBOMViewer.Blazor.Services;

public class SbomState
{
    public event Action? OnChange;

    private CycloneDXDocument? _cycloneDXData;
    public CycloneDXDocument? CycloneDXData
    {
        get => _cycloneDXData;
        set
        {
            _cycloneDXData = value;
            NotifyStateChanged();
        }
    }

    public string? CycloneDXFileName { get; set; }

    private SpdxDocument? _spdxData;
    public SpdxDocument? SpdxData
    {
        get => _spdxData;
        set
        {
            _spdxData = value;
            NotifyStateChanged();
        }
    }

    public string? SpdxFileName { get; set; }
    private void NotifyStateChanged() => OnChange?.Invoke();
}

