
# SPDX and SBOM-Tool JSON Viewer
[SBOM Viewer](https://sbomviewer.com)  
![SBOM Viewer](./sbomviewer.jpeg)



This web application provides an interactive and user-friendly interface for viewing and exploring parsed SPDX and SBOM-tool JSON files. Built with Blazor WebAssembly, the app loads and displays the data in a structured, readable format, making it easy for users to analyze software components, licenses, and dependencies directly in their browser.

## Current Version: 1.0.1

## Improvements

- Improved SPDX display by updating filenames to appear in an ordered list.

## Version: 1.0.0

## Key Features

- Displays parsed SPDX and SBOM-tool JSON data in a clean, hierarchical view.
- Fully client-side rendering using Blazor WebAssembly for fast and responsive interactions.
- Native Blazor components ensure a lightweight and maintainable user interface.
- Easy navigation of nested data structures for better exploration of complex JSON files.

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

- Upload or load your SPDX or SBOM-tool JSON file into the application.
- The data will be parsed and displayed in an easy-to-read format.
- Navigate through the data structure and view detailed information about software components, licenses, and dependencies.

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
