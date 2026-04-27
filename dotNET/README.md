# ProfileEvaluation

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)

A WPF desktop application for comparing radiation therapy beam profiles side-by-side using gamma analysis, flatness, symmetry, and off-axis ratio metrics.

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

## Requirements

- Windows with .NET Framework 4.8.1
- Visual Studio 2022 (for building)

---

## Building

Open `ProfileEvaluation.sln` in Visual Studio and build in Release or Debug configuration.
NuGet packages restore automatically:

| Package | Purpose |
|---------|---------|
| `OxyPlot.Wpf` | Interactive plot rendering and XAML export |
| `MathNet.Numerics` | Linear spline interpolation |
| `fo-dicom` (FellowOakDicom) | DICOM RT Dose file parsing |

---

## Usage

1. Click **Add File(s)** under **Reference Profile(s)** to load one or more files.
2. Select a scan from the list — field metrics update immediately in the left panel.
3. Repeat for **Test Profile(s)** on the right side.
4. Adjust gamma settings (DD, DTA, normalization, field restriction) in the lower-left panel.
5. Click **Compare** to run gamma analysis and overlay both profiles on the comparison plot.
6. Click **Print** to send a summary report to PDF (only available after a valid comparison).

### Field Centering

Check **Center field** on either side to shift the profile so its field midpoint aligns with x = 0.
The centering threshold (default 50%) sets the full-width level used to find the field edges.

---

## Project Layout

```
ProfileEvaluation/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs   # View and code-behind
├── ViewModel.cs                            # Gamma settings, comparison logic, main plot
├── ProfileModel.cs                         # Per-side data model and field metrics
├── DataReader.cs                           # MCC, OmniPro CSV, and DICOM parsing
├── DicomReader.cs                          # DICOM RT Dose pixel extraction (fo-dicom)
├── ProfileAnalysis.cs                      # Gamma, flatness, symmetry, off-axis ratio
├── XYData.cs                               # Paired position/dose array container
├── Extensions.cs                           # FindIndexOfMin/Max helpers
└── Common/                                 # WPF/MVVM base infrastructure
```

---

## Documentation

- [Architecture](docs/architecture.md) — component relationships, data flow, key design notes
- [API Reference](docs/api.md) — public interfaces for all core classes

---

## Gamma Analysis

The gamma index is computed using the standard formulation:

```
γ(r_ref) = min_j √[ (Δx / DTA)² + (ΔD / DD)² ]
```

where `Δx` is the spatial distance and `ΔD` is the dose difference between a reference point and each test point.

- **Global gamma**: ΔD is normalized to the maximum dose of the reference profile.
- **Local gamma**: ΔD is normalized to the dose at each reference point.
- Points outside the analysis region are excluded (reported as NaN).

Default parameters: DTA = 1 mm, DD = 2%, CAX normalization, analysis restricted to 80% of the FWHM field size.

---

## Related Project

**[ProfileEvaluationPy](ProfileEvaluationPy/README.md)** is a Python port of this tool offering the same analysis algorithms with a PyQt6 desktop app, a Streamlit web interface, and Docker support (cross-platform, no .NET required).  
See [`ProfileEvaluationPy/QUICKSTART.md`](ProfileEvaluationPy/QUICKSTART.md) to get started quickly.

---

## License

Internal tool — no license assigned.
