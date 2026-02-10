# Changelog

## [3.0.0] – Current

### New Features
- **CycloneDX 1.7 support** — lifecycles, component tags, OmniBOR IDs, SWHIDs, definitions/standards, declarations (assessors + claims), and formulation sections.
- **Auto-detect format** — the viewer now automatically identifies the SBOM format and version from the uploaded JSON file. No manual format selector needed.
- **Unsupported version handling** — uploading an unsupported version (e.g., CycloneDX 1.5, SPDX 2.3) shows a clear error message with the list of supported formats.
- **Supported formats display** — the upload toolbar shows badges for all supported formats (CycloneDX 1.6, CycloneDX 1.7, SPDX 2.2).
- **Sample SBOM files** — added `samples/` folder with ready-to-use CycloneDX 1.6, 1.7, and SPDX 2.2 test files.

### Improvements
- Removed the manual format selector dropdown for a simpler upload experience.
- Cleaned up legacy duplicate upload handler in MainLayout.
- New `SbomFormatDetector` service for lightweight JSON-based format detection.
- CycloneDX viewer conditionally shows 1.7 sections only when data is present.

---

## [2.0.0]

### Improvements
- Complete UI overhaul using **Fluent UI components** for a modern, consistent, and responsive look.
- **Automatic clearing** of previous SBOM data when a new file is uploaded, ensuring viewers display only the current file.
- Enhanced layout and styling for **SPDX and CycloneDX viewers**, improving readability and user experience.

---

## [1.0.1]

### Improvements
- Improved SPDX display by presenting filenames in a **clear, ordered list**.

---

## [1.0.0]

### Key Features
- Displays parsed **SPDX** and **CycloneDX** JSON data in a clean, hierarchical view.
- Fully **client-side rendering** using Blazor WebAssembly for fast and responsive interactions.
- Modern, consistent UI built with **Fluent UI components**.
- Lightweight and maintainable design using **native Blazor components**.
- Easy navigation of nested data structures for better exploration of complex SBOM files.
### Known Issues
- No major issues at the moment.
