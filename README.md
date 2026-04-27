# ProfileEvaluation

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A desktop application for comparing radiation therapy beam profiles side-by-side using gamma analysis, flatness, symmetry, and off-axis ratio metrics.

---

## Features

- Load and compare **Reference** and **Test** profiles from three file formats:
  - PTW MCC/Mephysto (`.mcc`)
  - DICOM RT Dose (`.dcm`) — extracts inplane and crossplane profiles
  - iba OmniPro Accept CSV (`.csv`)
- Per-profile metrics: **flatness** and **symmetry** (Varian and Elekta conventions), **off-axis ratio**
- Optional **field centering** at a configurable full-width threshold (default: FWHM)
- **Gamma index** comparison with configurable DTA, DD, normalization (CAX or max), global/local mode, and field-size restriction
- Gamma result summary: fraction passing (γ < 1), average γ, maximum γ
- **Print to PDF** via the system print dialog (includes settings, metrics, and comparison plot)

---

## Implementations

- [Windows w/ WPF](dotNET/README.md) for the primary WPF desktop application.
- **[Python w/ Qt6](python/README.md)** for a Python port of this tool offering the same analysis algorithms with a PyQt6 desktop app, a Streamlit web interface, and Docker support (cross-platform, no .NET required).  

---

### License: [MIT](LICENSE)
