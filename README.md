
# SPDX & CycloneDX JSON Viewer
*A modern web-based viewer for SPDX & CycloneDX SBOMs.*

[**SBOM Viewer**](https://sbomviewer.com)

![SBOM Viewer Screenshot](./sbomviewer_v2.jpeg)


SBOM Viewer is a web application built with Blazor WebAssembly that provides an interactive, user-friendly interface for viewing and exploring parsed SPDX and CycloneDX JSON files.

It loads data directly in the browser and presents it in a structured, hierarchical format—making it easy to analyze software components, licenses, and dependencies without additional tools.

## Latest Release

**Version 2.0.0** – Revamped UI and Fluent UI integration

- Modern, consistent UI using Fluent UI components
- Automatic clearing of previous SBOM when a new file is uploaded
- Enhanced SPDX and CycloneDX viewers for better readability

For the complete history of changes, see [CHANGELOG.md](./CHANGELOG.md).


## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/spdx-sbom-viewer.git
   ```

2. Navigate to the project directory:
   ```bash
   cd spdx-sbom-viewer
   ```

3. Restore the dependencies:
   ```bash
   dotnet restore
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

   This will launch the app in your browser at `https://localhost:5157`.

## Usage

- Upload or load your SPDX or CycloneDX JSON file into the application.
- The data will be parsed and displayed in a clean, easy-to-read format.
- Navigate through the structured view to explore detailed information about software components, licenses, and dependencies.

## Technology Stack

- **Blazor WebAssembly**: Used for building a responsive, client-side user interface.
- **C#**: For application logic and handling of JSON data.
- **JSON**: Used for parsing and displaying SPDX and SBOM-tool data.

## Contributing

We welcome contributions to improve the project! Feel free to fork the repository, open issues, or submit pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

- Thanks to the Blazor community for providing the framework that makes this project possible.
