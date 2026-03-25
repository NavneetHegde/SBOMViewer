# AI Chat — Sample Prompts

Useful prompts to ask about your SBOM once the AI model is loaded.

## Overview

- "Summarize this SBOM"
- "What format is this SBOM and how many components does it have?"
- "Give me a high-level overview of this software's dependencies"

## Dependencies

- "List the most important packages in this SBOM"
- "Are there any outdated packages?"
- "What ecosystems are represented (npm, NuGet, PyPI, etc.)?"
- "Do any packages appear to be duplicated?"
- "What are the top-level frameworks or libraries used?"

## Security

> These prompts work best after running the vulnerability scan.

- "Which packages have critical vulnerabilities?"
- "Summarize the vulnerability scan results"
- "What should I fix first based on the vulnerability findings?"
- "Are there any packages with known fixes available?"
- "How many vulnerabilities are there by severity?"

## Licensing & Compliance

- "What can you tell me about the licenses in this SBOM?"
- "Are there any components that might have restrictive licenses?"
- "Are all packages using permissive open-source licenses?"

## General Analysis

- "What does this software appear to do based on its dependencies?"
- "Are there any test or dev-only dependencies included?"
- "Does this SBOM look complete or is anything missing?"
- "How many unique ecosystems and package managers are involved?"

## Limitations

- The model is small (1.7B parameters) and runs locally in the browser.
- It only sees the top 20 components and the vulnerability summary (if scanned), not the full SBOM JSON.
- Answers are concise but may lack depth on specific packages.
- For detailed package analysis, cross-reference with the SBOM viewer's accordion sections.
