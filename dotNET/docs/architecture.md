# Architecture — ProfileEvaluation

## Overview

ProfileEvaluation is a WPF desktop application (.NET Framework 4.8.1) following the MVVM pattern.
It loads radiation therapy beam profile files (MCC, DICOM RT Dose, OmniPro CSV), displays them side-by-side as Reference and Test profiles, computes field quality metrics (flatness, symmetry, off-axis ratio), performs gamma index analysis, and prints a summary report.

---

## Component Map

```
MainWindow (View)
    │
    ├─ ViewModel (: ViewModelBase)          ← gamma settings, comparison, main plot
    │       ├─ LeftModel  : ProfileModel    ← Reference side
    │       ├─ RightModel : ProfileModel    ← Test side
    │       └─ PlotModel  : OxyPlot         ← combined gamma + profile overlay
    │
    └─ ProfileModel (: VPropertyChanged)
            ├─ MCCData : ObservableCollection<DataReader.MCCData>
            ├─ SelectedData : ObservableValue<MCCData>
            ├─ PlotModel : OxyPlot           ← per-side preview plot
            └─ Metrics (ObservableValue<double>): Flatness/Symmetry/OAR
```

---

## Data Flow

```
User selects file(s)
        │
        ▼
MainWindow.Button_AddFiles_Click
        │  dispatches by extension
        ├─ .mcc  → ProfileModel.ReadMccFile  → DataReader.ReadMCCData
        ├─ .dcm  → ProfileModel.ReadDicomFile → DataReader.ParseDicomToMcc → DicomReader
        └─ .csv  → ProfileModel.ReadOmniProCsvFile → DataReader.ParseOmniProCsvToMcc
                                │
                                ▼
                  DataReader.InterpolateAndSetMembers
                  (linear spline @ 0.5mm, produces CAX/Max/raw variants)
                                │
                                ▼
                  MCCData added to ProfileModel.MCCData collection
                                │
        User selects scan in ListView
                                │
                                ▼
                  ProfileModel.SelectedData.Value changes
                  ├─ CalculateFieldParameters()
                  │     └─ ProfileAnalysis.CalculateFlatnessAndSymmetry
                  │     └─ ProfileAnalysis.CalculateOffAxisRatio
                  └─ PlotMCCData() → per-side preview refreshed
                                │
        User clicks "Compare"
                                │
                                ▼
                  ViewModel.CompareData()
                  ├─ Normalize (CAX / max / none)
                  ├─ Optionally center via ProfileAnalysis.ShiftAndTruncateProfile
                  ├─ Optionally restrict to FWHM%
                  ├─ ProfileAnalysis.ComputeGamma(reference, test, DTA, DD)
                  ├─ Store GammaResultMax/Average/FractionLessThanOne
                  └─ Rebuild PlotModel series (analysis bounds, gamma curve, profile overlays)
                                │
        User clicks "Print"
                                │
                                ▼
                  MainWindow.PrintVisualToPdf
                  └─ FlowDocument with XamlWriter-cloned UI panels + OxyPlot XAML export
```

---

## Key Classes

### `DataReader` (`DataReader.cs`)

All parsing lives here. Produces `DataReader.MCCData` objects.

| Method | Format |
|--------|--------|
| `ReadMCCData(path)` | PTW MCC/Mephysto |
| `ParseDicomToMcc(path)` | DICOM RT Dose — delegates pixel extraction to `DicomReader` |
| `ParseOmniProCsvToMcc(path)` | iba OmniPro Accept CSV |
| `InterpolateAndSetMembers(ref MCCData)` | Linear spline + normalization (shared by all parsers) |

### `DataReader.MCCData`

Rich data transfer object produced by all parsers. Fields include:
- Scan metadata: `CurveType`, `Energy`, `Modality`, `Filter`, `SSD`, `ScanDepth`, `FieldInplane`, `FieldCrossplane`, `Gantry`, `Wedge*`
- Raw positions: `Pos` — `List<double[]>` of `[x, y]` pairs
- Interpolated variants: `Interpolated`, `InterpolatedCAX` (÷ CAX value), `InterpolatedMax` (÷ max value)
- Normalized arrays: `DataCAX`, `DataMAX`, `DataNON`, `DataMAN`
- `GetXData()` / `GetYData()` — return the interpolated CAX or max series depending on `CurveType`

### `DicomReader` (`DicomReader.cs`)

Wraps `fo-dicom`. Opens an RT Dose file, extracts `DoseGridScaling`, dimensions, and pixel data as a `double[frames, rows, cols]` matrix. Supports 16-bit and 32-bit pixel representations.

### `ProfileAnalysis` (`ProfileAnalysis.cs`)

Static methods only. No state.

| Method | Purpose |
|--------|---------|
| `ShiftAndTruncateProfile` | Center field on FWHM midpoint, trim symmetrically |
| `CalculateFlatnessAndSymmetry` | Varian and Elekta flatness + symmetry within ±limitToX |
| `CalculateOffAxisRatio` | OAR = 2·D_CAX / (D₋ + D₊) |
| `ComputeGamma` | O(n×m) gamma index, global or local normalization |

### `XYData` (`XYData.cs`)

Simple paired array container (X in mm, Y in dose). Helpers: `GetIndicesWithinX`, `GetYWhereXIsWithin`, `GetFieldSizeFWHM` (assumes Y≈1 at plateau).

### `ProfileModel` (`ProfileModel.cs`)

Per-side MVVM model. Subscribes to `SelectedData.OnChanged` and `CenterField.OnChanged` to trigger `CalculateFieldParameters` + `PlotMCCData` automatically.

### `ViewModel` (`ViewModel.cs`)

Orchestrator. Exposes all gamma settings as bindable properties; each setter sets `ComparisonInvalidated = true` so the UI overlays "invalid | re-compare" until `CompareData()` is called.

### Common infrastructure (`Common/`)

| Class | Role |
|-------|------|
| `VPropertyChanged` | Base `INotifyPropertyChanged` with `SetProperty<T>` helpers |
| `ViewModelBase` | Thin `VPropertyChanged` subclass adding virtual `Initialize()` |
| `ObservableValue<T>` | Single-value observable with `OnChanged` callback |
| `WindowBase<TViewModel>` | WPF window base providing typed `ViewModel` property |
| `InverseBooleanConverter` | XAML value converter for `!bool` bindings |

---

## Dependencies

| Dependency | Role |
|------------|------|
| `OxyPlot.Wpf` | Plot rendering and XAML export for print |
| `MathNet.Numerics` | `LinearSpline.Interpolate` for profile resampling |
| `fo-dicom` (FellowOak.Dicom) | DICOM RT Dose file reading |
| .NET Framework 4.8.1 WPF | UI framework, printing, file dialogs |

---

## Architectural Notes

- **No IoC container.** Dependencies are constructed inline or passed through constructors.
- **`ProfileModel` couples to `ViewModel`** via the static `ViewModel.InitializePlot()` call in its constructor. This works but inverts expected dependency direction; ideally the plot initialization logic would move to a shared utility.
- **`ComparisonInvalidated`** is the single gate controlling whether "Print" is available and whether the "invalid | re-compare" overlay is shown on the comparison plot.
- **Print path** deep-clones WPF UI elements via `XamlWriter.Save` / `XamlReader.Parse` and exports the OxyPlot model to a XAML canvas for inclusion in a `FlowDocument`.
