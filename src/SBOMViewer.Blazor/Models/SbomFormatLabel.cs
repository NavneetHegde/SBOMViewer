namespace SBOMViewer.Blazor.Models;

public static class SbomFormatLabel
{
    public static string For(SbomFormat? format) => format switch
    {
        SbomFormat.CycloneDX_1_5 => "CycloneDX 1.5",
        SbomFormat.CycloneDX_1_6 => "CycloneDX 1.6",
        SbomFormat.CycloneDX_1_7 => "CycloneDX 1.7",
        SbomFormat.SPDX_2_2      => "SPDX 2.2",
        SbomFormat.SPDX_2_3      => "SPDX 2.3",
        SbomFormat.SPDX_3_0      => "SPDX 3.0.1",
        _                        => "Unknown"
    };
}
