using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Holds the two documents being compared. Kept separate from <see cref="SbomState"/> so the
/// single-document viewer's lifecycle stays simple.
///
/// Both documents live in memory only, for the lifetime of the browser tab — nothing is written
/// to storage and nothing leaves the browser.
/// </summary>
public class SbomCompareState
{
    public event Action? OnChange;

    private Slot _baseline = Slot.Empty;
    private Slot _current  = Slot.Empty;
    private SbomDiff? _diff;

    public JsonDocument? BaselineDocument => _baseline.Document;
    public JsonDocument? CurrentDocument  => _current.Document;
    public string? BaselineFileName => _baseline.FileName;
    public string? CurrentFileName  => _current.FileName;
    public SbomFormat? BaselineFormat => _baseline.Format;
    public SbomFormat? CurrentFormat  => _current.Format;

    public bool HasBaseline => _baseline.Document is not null;
    public bool HasCurrent  => _current.Document is not null;
    public bool IsReady     => HasBaseline && HasCurrent;

    /// <summary>Computed once both slots are filled; null until then.</summary>
    public SbomDiff? Diff => _diff;

    public int BaselineComponentCount => _baseline.ComponentCount;
    public int CurrentComponentCount  => _current.ComponentCount;

    public void SetBaseline(SbomLoadResult result) => Set(ref _baseline, result);
    public void SetCurrent(SbomLoadResult result)  => Set(ref _current,  result);

    private void Set(ref Slot slot, SbomLoadResult result)
    {
        if (!result.Success) return;

        slot.Document?.Dispose();
        slot = new Slot(
            result.Document,
            result.Format,
            result.FileName,
            ComponentRowExtractor.Extract(result.Document!.RootElement, result.Format).Count);
        Recompute();
    }

    public void ClearBaseline() { _baseline.Document?.Dispose(); _baseline = Slot.Empty; Recompute(); }
    public void ClearCurrent()  { _current.Document?.Dispose();  _current  = Slot.Empty; Recompute(); }

    /// <summary>
    /// Exchanges the two slots, so the diff runs in the opposite direction. Recovers from files
    /// dropped in the wrong order without re-uploading either of them.
    ///
    /// No document is created or disposed here — the two slots simply change places — so this is
    /// safe with one slot filled, or with both.
    /// </summary>
    public void Swap()
    {
        if (!HasBaseline && !HasCurrent) return;

        (_baseline, _current) = (_current, _baseline);
        Recompute();
    }

    public void Clear()
    {
        _baseline.Document?.Dispose();
        _current.Document?.Dispose();
        _baseline = Slot.Empty;
        _current  = Slot.Empty;
        _diff     = null;
        OnChange?.Invoke();
    }

    private void Recompute()
    {
        _diff = IsReady
            ? SbomDiffService.Compare(
                _baseline.Document!.RootElement, _baseline.Format,
                _current.Document!.RootElement,  _current.Format)
            : null;

        OnChange?.Invoke();
    }

    private readonly record struct Slot(JsonDocument? Document, SbomFormat? Format, string? FileName, int ComponentCount)
    {
        public static Slot Empty => new(null, null, null, 0);
    }
}
