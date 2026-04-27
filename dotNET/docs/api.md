# API Reference — ProfileEvaluation

This document describes the public and internal interfaces of the core library classes.
The project is a single-executable WPF application; these classes are not packaged as a reusable library,
but their APIs are documented here for contributor reference.

---

## `DataReader`

**Namespace:** `ProfileEvaluation`  
**File:** `DataReader.cs`

### Static Methods

#### `ReadMCCData(string filePath, double normalValDist = 0, double normalValPerc = 100) → List<MCCData>`

Parses a PTW MCC/Mephysto text file. Returns one `MCCData` per `BEGIN_SCAN` block.

| Parameter | Description |
|-----------|-------------|
| `filePath` | Absolute path to the `.mcc` file |
| `normalValDist` | Reserved, unused in current implementation |
| `normalValPerc` | Reserved, unused in current implementation |

**Throws:** `FormatException` (propagated) if a data point line contains non-numeric values.

---

#### `ParseDicomToMcc(string fileName) → IEnumerable<MCCData>`

Opens a DICOM RT Dose file, reads the dose matrix, and produces two `MCCData` entries:
one `INPLANE` (central row) and one `CROSSPLANE` (central column) profile.

| Parameter | Description |
|-----------|-------------|
| `fileName` | Absolute path to the `.dcm` file |

**Throws:** `ArgumentException` if the file is not RT Dose Storage or lacks pixel data.  
**Note:** Field size is fixed at 100×100mm in the generated `MCCData`; pixel spacing is read from the DICOM tag.

---

#### `ParseOmniProCsvToMcc(string filePath) → List<MCCData>`

Parses an iba OmniPro Accept CSV export. Each `Measurement time:` line starts a new block.
Scan type (`Inline`/`Crossline`/`Beam`/diagonal) determines which column becomes the X axis.

| Parameter | Description |
|-----------|-------------|
| `filePath` | Absolute path to the `.csv` file |

**Throws:** `Exception` (propagated from outer caller) on malformed date or field-size lines.  
**Silently skips** data point lines that fail numeric parsing (logs to `Console`).

---

#### `InterpolateAndSetMembers(ref MCCData scanData)`

Shared post-processing step called by all three parsers. Applies `MathNet.Numerics.LinearSpline`
to `scanData.Pos` at 0.5mm steps and writes:

| Output field | Content |
|-------------|---------|
| `Interpolated` | Raw interpolated `[x, y]` pairs |
| `InterpolatedCAX` | `[x, y/caxValue]` pairs (CAX-normalized) |
| `InterpolatedMax` | `[x, y/maxValue]` pairs (max-normalized) |
| `DataCAX` | Array of `y/caxValue` at original sample positions |
| `DataMAX` | Array of `y/maxValue` at original sample positions |
| `DataNON` | Raw y values (no normalization) |
| `DataMAN` | Same as `DataNON`; reserved for manual normalization |

CAX value: raw sample at x=0; if no sample at x=0 exactly, falls back to `yValues.Max()`.

---

#### `GetPixelPositionsCentered(int n, double spacing) → double[]`

Returns an array of `n` positions centered around 0 with the given `spacing` (mm).

---

### `DataReader.MCCData`

**Key properties consumed by the rest of the application:**

| Property | Type | Description |
|----------|------|-------------|
| `GetInfoString` | `string` (get-only) | Human-readable summary shown in the ListView |
| `GetXData()` | `List<double>` | Returns `InterpolatedCAX[*][0]` (positions) |
| `GetYData()` | `List<double>` | Returns `InterpolatedMax[*][1]` for `Beam` curves; `InterpolatedCAX[*][1]` otherwise |
| `GetFieldSizeInScanDirection()` | `double` | Returns `FieldCrossplane` or `FieldInplane` based on `CurveType` |
| `CurveType` | `string` | Scan type string: `INPLANE`, `CROSSPLANE`, `Inline`, `Crossline`, `Beam`, etc. |
| `ScanNumber` | `int` | Zero-based index within the source file |
| `FileName` | `string` | Base filename of the source file |

---

## `DicomReader`

**Namespace:** `ProfileEvaluation`  
**File:** `DicomReader.cs`

### Constructor

```csharp
DicomReader(string filePath)
DicomReader(FileStream dicomStream)
```

Lazy-loads the DICOM file on first access to `DicomFile`.

### Instance Methods

#### `ReadDoseMatrix() → double[,,]`

Returns a `[frames, rows, columns]` dose matrix scaled by `DoseGridScaling`. Delegates to the static overload.

#### `GetPixelSpacing() → double[]`

Returns `[rowSpacing, colSpacing]` in mm from `DicomTag.PixelSpacing`. May return null if the tag is absent.

### Static Methods

#### `ReadDoseMatrix(DicomFile dicom) → double[,,]`

Reads and validates an RT Dose DICOM file. Supports 16-bit and 32-bit pixel representations for 2D and 3D dose grids.

**Throws:** `ArgumentException` for non-RT-Dose files or missing pixel data.  
**Throws:** `NotImplementedException` for 8-bit (OB) pixel data.

---

## `ProfileAnalysis`

**Namespace:** `ProfileEvaluation`  
**File:** `ProfileAnalysis.cs`

All methods are static and stateless.

### `ShiftAndTruncateProfile(XYData profile, double fullWidthAt = 0.5) → XYData`

Centers a profile on the midpoint of `fullWidthAt`×Y_max and trims it symmetrically.

| Parameter | Description |
|-----------|-------------|
| `profile` | Input `XYData`; X must be sorted ascending |
| `fullWidthAt` | Fraction of max defining the field edge (0–1). Default = 0.5 (FWHM) |

**Returns:** New `XYData` centered at 0.  
**Throws:** `ArgumentOutOfRangeException` if `fullWidthAt` not in [0,1].  
**Throws:** `InvalidOperationException` if the FWHM boundary cannot be determined.

---

### `CalculateFlatnessAndSymmetry(XYData data, double limitToX = double.MaxValue) → (VarianSymmetry, VarianFlatness, ElektaSymmetry, ElektaFlatness)`

Computes four beam quality metrics within ±`limitToX` mm of the central axis.

| Metric | Formula |
|--------|---------|
| Varian Flatness | (Dmax − Dmin) / (Dmax + Dmin) |
| Varian Symmetry | max \|D(−x) − D(x)\| / D_CAX |
| Elekta Flatness | Dmax / Dmin |
| Elekta Symmetry | max(D(−x)/D(x), D(x)/D(−x)) |

All values are dimensionless ratios (multiply by 100 for percent in the UI).

---

### `CalculateOffAxisRatio(XYData data) → double`

Calculates OAR at a position derived from the field size:
- fields < 100mm: 60% of half-field
- fields ≥ 100mm: 80% of half-field

`OAR = 2·D_CAX / (D(−x) + D(+x))`

---

### `CalculateOffAxisRatio(XYData data, double atDistanceFromCax) → double`

Same formula evaluated at a specific distance from the central axis.

---

### `ComputeGamma(XYData reference, XYData test, double dta, double dd, bool globalGamma = true, double limitToX = double.MaxValue) → double[]`

Computes per-point gamma index for the reference profile.

| Parameter | Description |
|-----------|-------------|
| `reference` | Reference profile (normalized) |
| `test` | Test profile (normalized to same scale) |
| `dta` | Distance-to-agreement criterion in mm |
| `dd` | Dose-difference criterion as a fraction (e.g. `0.02` for 2%) |
| `globalGamma` | `true` = global gamma (DD normalized to reference maximum); `false` = local gamma |
| `limitToX` | Analysis region half-width in mm; points outside are set to `NaN` |

**Returns:** `double[]` of length `reference.X.Length`.  
**Complexity:** O(n_ref × n_test) — brute-force minimum search over all test points per reference point.

---

## `XYData`

**Namespace:** `ProfileEvaluation`  
**File:** `XYData.cs`

Simple container for a paired (X, Y) profile.

| Member | Description |
|--------|-------------|
| `X` | `double[]` — positions in mm, sorted ascending |
| `Y` | `double[]` — dose/signal values |
| `GetIndicesWithinX(limitToX)` | Indices where \|X[i]\| ≤ limitToX |
| `GetYWhereXIsWithin(limitToX)` | Y values filtered to the symmetric region |
| `GetFieldSizeFWHM()` | Distance between outermost points with Y ≥ 0.5 (assumes normalized data) |

---

## `ProfileModel`

**Namespace:** `ProfileEvaluation`  
**File:** `ProfileModel.cs`

Per-side MVVM model exposed to the view via `ViewModel.LeftModel` / `ViewModel.RightModel`.

| Member | Type | Description |
|--------|------|-------------|
| `ProfileType` | `ProfileUse` enum | `Reference` or `Test` — controls plot line style |
| `MCCData` | `ObservableCollection<MCCData>` | All scans loaded for this side |
| `SelectedData` | `ObservableValue<MCCData>` | Currently selected scan; triggers metrics + plot refresh |
| `FlatnessVarian/Elekta` | `ObservableValue<double>` | Flatness in % |
| `SymmetryVarian/Elekta` | `ObservableValue<double>` | Symmetry in % |
| `OffAxisRatio` | `ObservableValue<double>` | Off-axis ratio |
| `CenterField` | `ObservableValue<bool>` | Whether to apply field centering before analysis |
| `CenterFieldAtFWOf` | `ObservableValue<double>` | Full-width threshold in % (default 50 = FWHM) |
| `PlotModel` | `OxyPlot.PlotModel` | Per-side preview plot (gamma axis hidden) |
| `ReadMccFile(path)` | method | Loads MCC file into `MCCData` |
| `ReadDicomFile(path)` | method | Loads DICOM RT Dose into `MCCData` |
| `ReadOmniProCsvFile(path)` | method | Loads OmniPro CSV into `MCCData` |
| `CalculateFieldParameters()` | method | Recomputes all metrics for `SelectedData` |
| `PlotMCCData(clearFirst)` | method | Refreshes the per-side preview plot |

---

## `ViewModel`

**Namespace:** `ProfileEvaluation`  
**File:** `ViewModel.cs`

Main application view model. All properties implement `INotifyPropertyChanged` via `SetProperty`.

### Gamma Settings (bindable)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GammaDTA` | `double` | 1.0 | Distance-to-agreement (mm). Must be > 0 |
| `GammaDD` | `double` | 2.0 | Dose-difference (%). Must be > 0. Divided by 100 before passing to `ComputeGamma` |
| `GammaNormalizeCAX` | `bool` | `true` | Normalize profiles to central-axis dose before comparison |
| `GammaNormalizeMax` | `bool` | `false` | Normalize profiles to maximum dose before comparison |
| `GammaGlobalComparison` | `bool` | `false` | Use global (vs. local) gamma formulation |
| `GammaRestrictToFieldSize` | `bool` | `true` | Restrict analysis to a percentage of the FWHM field size |
| `GammaRestrictToFieldSizePercent` | `double` | 80 | Analysis region as % of half-field size (0–1000) |

### Gamma Results (read-only, bindable)

| Property | Description |
|----------|-------------|
| `GammaResultFractionLessThanOne` | % of gamma values < 1.0 |
| `GammaResultAverage` | Mean gamma (excluding NaN) |
| `GammaResultMax` | Maximum gamma (excluding NaN) |
| `ComparisonInvalidated` | `ObservableValue<bool>` — true whenever settings or selections change |

### Key Methods

| Method | Description |
|--------|-------------|
| `CompareData()` | Runs the full comparison pipeline; updates results and plot |
| `InitializePlot(PlotModel)` | Static — sets up standard 3-axis plot configuration |
| `LineSeriesDefault_Gamma(dd, dta)` | Returns a styled `LineSeries` for the gamma curve |
| `LineSeriesDefault_Reference` | Black solid `LineSeries` for the reference profile |
| `LineSeriesDefault_Test` | Red dashed `LineSeries` for the test profile |
| `LineSeriesDefault_AxLine` | Steel-blue dashed `LineSeries` for analysis boundary markers |
