using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using MathNet.Numerics.Interpolation;

namespace ProfileEvaluation
{
	/// <summary>
	/// Parses beam profile data from three file formats into a normalized <see cref="MCCData"/>
	/// representation with linear-spline interpolation at 0.5mm steps.
	/// Supported formats:
	/// <list type="bullet">
	///   <item><b>MCC</b> — PTW BeamScan / Mephysto text format (<see cref="ReadMCCData"/>)</item>
	///   <item><b>DICOM RT Dose</b> — extracts inplane and crossplane profiles via <see cref="DicomReader"/> (<see cref="ParseDicomToMcc"/>)</item>
	///   <item><b>OmniPro CSV</b> — iba OmniPro Accept CSV export (<see cref="ParseOmniProCsvToMcc"/>)</item>
	/// </list>
	/// All parsers call <see cref="InterpolateAndSetMembers"/> which produces four normalization
	/// variants: CAX-normalized, max-normalized, raw, and manual (DataCAX/DataMAX/DataNON/DataMAN).
	/// </summary>
	public class DataReader
	{
		const double InterpolationStep = 0.5;

		public class MCCData
		{
			public string FileName { get; set; }
			public int ScanNumber { get; set; }
			public string TaskName { get; set; }
			public string Program { get; set; }
			public string Comment { get; set; }
			public DateTime MeasurementDate { get; set; }
			public string RadiationDevice { get; set; }
			public string Modality { get; set; }
			public double? Isocenter { get; set; }
			public string InplaneAxis { get; set; }
			public string CrossplaneAxis { get; set; }
			public string DepthAxis { get; set; }
			public string InplaneAxisDir { get; set; }
			public string CrossplaneAxisDir { get; set; }
			public string DepthAxisDir { get; set; }
			public double? Energy { get; set; }
			public string Filter { get; set; }
			public double? SAD { get; set; }
			public double? SSD { get; set; }
			public string Wedge { get; set; }
			public double? WedgeAngle { get; set; }
			public string WedgeType { get; set; }
			public double? FieldInplane { get; set; }
			public double? FieldCrossplane { get; set; }
			public string FieldType { get; set; }
			public double? Gantry { get; set; }
			public int GantryUprightPosition { get; set; }
			public string GantryRotation { get; set; }
			public double? CollAngle { get; set; }
			public double? CollOffsetInplane { get; set; }
			public double? CollOffsetCrossplane { get; set; }
			public string ScanDeviceSetup { get; set; }
			public string Electrometer { get; set; }
			public string Detector { get; set; }
			public string DetectorReference { get; set; }
			public string RefFieldDefined { get; set; }
			public string CurveType { get; set; }
			public double? ScanDepth { get; set; }
			public double? ScanOffaxisInplane { get; set; }
			public double? ScanOffaxisCrossplane { get; set; }
			public double? ScanAngle { get; set; }
			public string ScanDiagonal { get; set; }
			public string ScanDirection { get; set; }
			public string MeasPreset { get; set; }
			public string MeasUnit { get; set; }
			public double? InclinationAngle { get; set; }


			public string Name { get; set; }
			public string ScanOffAxisInPlane { get; set; }
			public string ScanOffAxisCrossPlane { get; set; }
			public string DataType { get; set; }
			public string DataUnit { get; set; }

			//public string FieldSize { get; set; }
			public List<double[]> Pos { get; set; } = new List<double[]>();
			public double[] DataCAX { get; set; }
			public double[] DataMAX { get; set; }
			public double[] DataNON { get; set; }
			public double[] DataMAN { get; set; }
			public List<double[]> Interpolated { get; set; }
			public List<double[]> InterpolatedCAX { get; set; }
			public List<double[]> InterpolatedMax { get; set; }


			public List<double> GetXData() => InterpolatedCAX.Select(x => x[0]).ToList();
			public List<double> GetYData()
			{
				if (CurveType == "Beam")
					return InterpolatedMax.Select(x => x[1]).ToList();
				else
					return InterpolatedCAX.Select(x => x[1]).ToList();
			}

			public double GetFieldSizeInScanDirection() => CurveType.ToLower().Contains("crossplane") ? (FieldCrossplane ?? 0) : (FieldInplane ?? 0);

			public string GetInfoString => $"{Name} #{ScanNumber} <{CurveType}> {Modality}{Energy}{Filter} FS{FieldCrossplane}x{FieldInplane} SSD{SSD} d{ScanDepth} ACC={Wedge}x{WedgeAngle ?? 0}x{WedgeType ?? ""}";
		}

		/// <summary>
		/// Parses a PTW MCC/Mephysto file and returns one <see cref="MCCData"/> per BEGIN_SCAN block.
		/// Metadata fields (energy, SSD, field size, curve type, etc.) are extracted from key=value
		/// lines; numeric data points from BEGIN_DATA/END_DATA blocks.
		/// Each scan is interpolated at <see cref="InterpolationStep"/> mm spacing via linear spline.
		/// </summary>
		public static List<MCCData> ReadMCCData(string filePath, double normalValDist = 0, double normalValPerc = 100)
		{
			var mccData = new List<MCCData>();

			var dataRegex = new Regex(@"(?<!#.*?)\s*(?<value>[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?)", RegexOptions.Compiled);

			using (var reader = new StreamReader(filePath))
			{
				string line;
				int scanCount = 0;
				while ((line = reader.ReadLine()) != null)
				{
					if (line.Contains("BEGIN_SCAN"))
					{
						var tempName = Path.GetFileNameWithoutExtension(filePath).Replace("mcc", "");
						var scanData = new MCCData
						{
							FileName = Path.GetFileName(filePath),
							ScanNumber = scanCount++,
							Name = tempName,
							Pos = new List<double[]>()
						};

						while ((line = reader.ReadLine()) != null && !line.Contains("END_SCAN"))
						{
							if (line.Contains("="))
							{
								var parts = line.Split('=');
								var key = parts[0].Trim();
								var value = parts[1].Trim();

								switch (key)
								{
									case "TASK_NAME":
										scanData.TaskName = value;
										break;
									case "PROGRAM":
										scanData.Program = value;
										break;
									case "COMMENT":
										scanData.Comment = value;
										break;
									case "MEAS_DATE":
										if (DateTime.TryParse(value, out DateTime measDate))
											scanData.MeasurementDate = measDate;
										break;
									case "LINAC":
										scanData.RadiationDevice = value;
										break;
									case "MODALITY":
										scanData.Modality = value;
										break;
									case "ISOCENTER":
										if (double.TryParse(value, out double isocenter))
											scanData.Isocenter = isocenter;
										break;
									case "INPLANE_AXIS":
										scanData.InplaneAxis = value;
										break;
									case "CROSSPLANE_AXIS":
										scanData.CrossplaneAxis = value;
										break;
									case "DEPTH_AXIS":
										scanData.DepthAxis = value;
										break;
									case "INPLANE_AXIS_DIR":
										scanData.InplaneAxisDir = value;
										break;
									case "CROSSPLANE_AXIS_DIR":
										scanData.CrossplaneAxisDir = value;
										break;
									case "DEPTH_AXIS_DIR":
										scanData.DepthAxisDir = value;
										break;
									case "ENERGY":
										if (double.TryParse(value, out double energy))
											scanData.Energy = energy;
										break;
									case "FILTER":
										scanData.Filter = value;
										break;
									case "SSD":
										if (double.TryParse(value, out double ssd))
											scanData.SSD = ssd;
										break;
									case "WEDGE":
										scanData.Wedge = value;
										break;
									case "WEDGE_ANGLE":
										if (double.TryParse(value, out double wedgeAngle))
											scanData.WedgeAngle = wedgeAngle;
										break;
									case "WEDGE_TYPE":
										scanData.WedgeType = value;
										break;
									case "FIELD_INPLANE":
										if (double.TryParse(value, out double fieldInplane))
											scanData.FieldInplane = fieldInplane;
										break;
									case "FIELD_CROSSPLANE":
										if (double.TryParse(value, out double fieldCrossplane))
											scanData.FieldCrossplane = fieldCrossplane;
										break;
									case "FIELD_TYPE":
										scanData.FieldType = value;
										break;
									case "GANTRY":
										if (double.TryParse(value, out double gantry))
											scanData.Gantry = gantry;
										break;
									case "GANTRY_UPRIGHT_POSITION":
										if (int.TryParse(value, out int gantryUprightPosition))
											scanData.GantryUprightPosition = gantryUprightPosition;
										break;
									case "GANTRY_ROTATION":
										scanData.GantryRotation = value;
										break;
									case "COLL_ANGLE":
										if (double.TryParse(value, out double collAngle))
											scanData.CollAngle = collAngle;
										break;
									case "COLL_OFFSET_INPLANE":
										if (double.TryParse(value, out double collOffsetInplane))
											scanData.CollOffsetInplane = collOffsetInplane;
										break;
									case "COLL_OFFSET_CROSSPLANE":
										if (double.TryParse(value, out double collOffsetCrossplane))
											scanData.CollOffsetCrossplane = collOffsetCrossplane;
										break;
									case "SCAN_DEVICE_SETUP":
										scanData.ScanDeviceSetup = value;
										break;
									case "ELECTROMETER":
										scanData.Electrometer = value;
										break;
									case "DETECTOR":
										scanData.Detector = value;
										break;
									case "REF_FIELD_DEFINED":
										scanData.RefFieldDefined = value;
										break;
									case "SCAN_CURVETYPE":
										scanData.CurveType = value;
										break;
									case "SCAN_DEPTH":
										if (double.TryParse(value, out double scanDepth))
											scanData.ScanDepth = scanDepth;
										break;
									case "SCAN_OFFAXIS_INPLANE":
										if (double.TryParse(value, out double scanOffaxisInplane))
											scanData.ScanOffaxisInplane = scanOffaxisInplane;
										break;
									case "SCAN_OFFAXIS_CROSSPLANE":
										if (double.TryParse(value, out double scanOffaxisCrossplane))
											scanData.ScanOffaxisCrossplane = scanOffaxisCrossplane;
										break;
									case "SCAN_ANGLE":
										if (double.TryParse(value, out double scanAngle))
											scanData.ScanAngle = scanAngle;
										break;
									case "SCAN_DIAGONAL":
										scanData.ScanDiagonal = value;
										break;
									case "SCAN_DIRECTION":
										scanData.ScanDirection = value;
										break;
									case "MEAS_PRESET":
										scanData.MeasPreset = value;
										break;
									case "MEAS_UNIT":
										scanData.MeasUnit = value;
										scanData.DataUnit = value;
										break;
									case "INCLINATION_ANGLE":
										if (double.TryParse(value, out double inclinationAngle))
											scanData.InclinationAngle = inclinationAngle;
										break;
								}
							}
							else if (line.Contains("BEGIN_DATA"))
							{
								var tempMat = new List<double[]>();
								while ((line = reader.ReadLine()) != null && !line.Contains("END_DATA"))
								{
									var matches = dataRegex.Matches(line);
									if (matches.Count == 2)
									{
										tempMat.Add(new double[]
										{
												double.Parse(matches[0].Groups["value"].Value),
												double.Parse(matches[1].Groups["value"].Value)
										});
									}
								}

								// Store positions and data
								scanData.Pos = tempMat.Select(row => new[] { row[0], row[1] }).ToList();

								InterpolateAndSetMembers(ref scanData);
							}
						} // while ((line = reader.ReadLine()) != null && !line.Contains("END_SCAN"))

						mccData.Add(scanData);
					}
				} // while
			}

			return mccData;
		}

		public static double[] GetPixelPositionsCentered(int n, double spacing)
		{
			double[] pixelPositions = new double[n];

			// Calculate the starting position
			double halfWidth = (n - 1) * spacing / 2.0;

			// Fill the array with positions centered around 0
			for (int i = 0; i < n; i++)
				pixelPositions[i] = i * spacing - halfWidth;

			return pixelPositions;
		}

		/// <summary>
		/// Applies linear spline interpolation to <see cref="MCCData.Pos"/> at <see cref="InterpolationStep"/> mm steps
		/// and populates all derived data members: Interpolated, InterpolatedCAX, InterpolatedMax,
		/// DataCAX, DataMAX, DataNON, and DataMAN.
		/// CAX value is taken from the raw sample nearest to position 0; falls back to max if absent.
		/// </summary>
		public static void InterpolateAndSetMembers(ref MCCData scanData)
		{
			// Interpolation
			var xValues = scanData.Pos.Select(p => p[0]).ToArray();
			var yValues = scanData.Pos.Select(p => p[1]).ToArray();
			var spline = LinearSpline.Interpolate(xValues, yValues);

			// Generate interpolated data
			var interpolatedData = new List<double[]>();
			double startX = xValues.Min();
			double endX = xValues.Max();
			//double InterpolationStep = 0.1; // Interpolation density

			for (double x = startX; x <= endX; x += InterpolationStep)
			{
				interpolatedData.Add(new[] { x, spline.Interpolate(x) });
			}

			scanData.Interpolated = interpolatedData;

			// CAX Normalization
			var caxIndex = scanData.Pos.FindIndex(p => p[0] == 0);
			double caxValue = caxIndex >= 0 ? yValues[caxIndex] : yValues.Max();
			scanData.DataCAX = yValues.Select(y => y / caxValue).ToArray();

			// Interpolated CAX Normalization
			scanData.InterpolatedCAX = interpolatedData
				.Select(p => new[] { p[0], p[1] / caxValue })
				.ToList();

			// MAX Normalization
			double maxVal = yValues.Max();
			scanData.DataMAX = yValues.Select(y => y / maxVal).ToArray();
			//
			scanData.InterpolatedMax = interpolatedData
				.Select(p => new[] { p[0], p[1] / maxVal })
				.ToList();

			// No Normalization
			scanData.DataNON = yValues;
			// Manual Normalization
			scanData.DataMAN = yValues;
		}

		public static IEnumerable<MCCData> ParseDicomToMcc(string fileName)
		{
			var dicomReader = new DicomReader(fileName);

			double[,,] doseMatrix = dicomReader.ReadDoseMatrix();

			int rows = doseMatrix.GetLength(1);
			int columns = doseMatrix.GetLength(2);
			double[] pixelSpacing = dicomReader.GetPixelSpacing();

			var posCross = GetPixelPositionsCentered(columns, pixelSpacing[1])
				.Select((v, i) => new double[] { v, doseMatrix[0, doseMatrix.GetLength(1) / 2, i] })
				.ToList();
			var posIn = GetPixelPositionsCentered(rows, pixelSpacing[0])
				.Select((v, i) => new double[] { v, doseMatrix[0, i, doseMatrix.GetLength(2) / 2] })
				.ToList();

			var scans = new List<MCCData>();

			int scanNumber = 0;
			{
				var data = new MCCData()
				{
					FileName = Path.GetFileName(fileName),
					Name = Path.GetFileNameWithoutExtension(fileName),
					ScanNumber = scanNumber++,
					CurveType = "INPLANE",
					Modality = "",
					Energy = 0,
					FieldCrossplane = 100, // TBD
					FieldInplane = 100,
					SSD = 1000,
					ScanDepth = 100,
					Wedge = "",
					WedgeAngle = null,
					WedgeType = null,
					Pos = posIn,
				};
				InterpolateAndSetMembers(ref data);
				scans.Add(data);
			}
			{
				var data = new MCCData()
				{
					FileName = Path.GetFileName(fileName),
					Name = Path.GetFileNameWithoutExtension(fileName),
					ScanNumber = scanNumber++,
					CurveType = "CROSSPLANE",
					Modality = "",
					Energy = 0,
					FieldCrossplane = 100, // TBD
					FieldInplane = 100,
					SSD = 1000,
					ScanDepth = 100,
					Wedge = "",
					WedgeAngle = null,
					WedgeType = null,
					Pos = posCross,
				};
				InterpolateAndSetMembers(ref data);
				scans.Add(data);
			}

			return scans;
		}

		public static List<MCCData> ParseOmniProCsvToMcc(string filePath)
		{
			var blocks = new List<MCCData>();

			int scanNumber = 0;
			MCCData currentBlock = null;
			bool readingPoints = false;
			var depths = new List<double>();
			foreach (var line in File.ReadLines(filePath))
			{
				var parts = Regex.Split(line, @":;|;")
					.Select(p => p.Trim())
					.Where(p => p.Length > 0)
					.ToArray();
				//line.Split(';', ':').Select(p => p.Trim()).ToArray();

				if (parts.Length < 1) continue; // Skip empty or malformed lines

				if (parts[0].StartsWith("Measurement time"))
				{
					if (currentBlock != null)
					{
						InterpolateAndSetMembers(ref currentBlock);
						currentBlock.ScanDepth = depths.Distinct().Count() == 1 ? depths.FirstOrDefault() : -1;
						blocks.Add(currentBlock); // Save previous block
					}
					currentBlock = new MCCData()
					{
						FileName = Path.GetFileName(filePath),
						Name = Path.GetFileNameWithoutExtension(filePath),
						ScanNumber = scanNumber++,
						MeasurementDate = DateTime.Parse(parts[1], CultureInfo.InvariantCulture),
					};
					depths.Clear();
					readingPoints = false;
				}
				else if (currentBlock != null) // Ensure a block is initialized
				{
					if (line.Trim().ToLowerInvariant().Contains("points [mm]"))
					{
						readingPoints = true;
					}
					switch (parts[0])
					{
						case "Radiation device": currentBlock.RadiationDevice = parts[1]; break;
						case "Energy":
							{
								if (parts[1].ToUpperInvariant().Contains("MEV"))
								{
									currentBlock.Modality = "EL";
									if (parts[1].ToUpperInvariant().Contains("FFF"))
										currentBlock.Filter = "HDTSE";
								}
								else if (parts[1].ToUpperInvariant().Contains("MV"))
								{
									currentBlock.Modality = "X";
									if (parts[1].ToUpperInvariant().Contains("FFF"))
										currentBlock.Filter = "FFF";
								}
								else
									currentBlock.Modality = "?";
								var m = Regex.Match(parts[1], @"(\d+).*?");
								if (m.Success && double.TryParse(m.Groups[0].Value, out double energy)) currentBlock.Energy = energy;
							}
							break;
						//case "Controller": currentBlock.Controller = parts[1]; break;
						//case "Measurement device": currentBlock.MeasurementDevice = parts[1]; break;
						case "Field detector": currentBlock.Detector = parts[1]; break;
						case "Reference detector": currentBlock.DetectorReference = parts[1]; break;
						case "SAD": currentBlock.SAD = double.Parse(parts[1].Replace("mm", "").Trim()); break;
						case "SSD": currentBlock.SSD = double.Parse(parts[1].Replace("mm", "").Trim()); break;
						case "Field size":
							{
								var m = Regex.Match(parts[1], @"(\d+)\s*x\s*(\d+)\s*mm");
								currentBlock.FieldInplane = double.TryParse(m.Groups[1].Value, out var fsradial) ? fsradial : 0;
								currentBlock.FieldCrossplane = double.TryParse(m.Groups[2].Value, out var fstransverse) ? fstransverse : 0;
							}
							break;
						case "Gantry angle": currentBlock.Gantry = double.TryParse(parts[1], out var gantry) ? gantry : 0; break;
						//case "Measurement medium": currentBlock.MeasurementMedium = parts[1]; break;
						case "Scan type": currentBlock.CurveType = parts[1]; break;
						//case "Scan mode": currentBlock.ScanMode = parts[1]; break;
						case "Wedge": currentBlock.Wedge = parts[1]; break;
						case "Points [mm]": readingPoints = true; break;
						default:
							if (readingPoints && parts.Length == 6)
							{
								if (string.Equals(parts[0], "Inline", StringComparison.InvariantCultureIgnoreCase))
									continue;
								try
								{
									var Inline = double.Parse(parts[0], CultureInfo.InvariantCulture);
									var Crossline = double.Parse(parts[1], CultureInfo.InvariantCulture);
									var Depth = double.Parse(parts[2], CultureInfo.InvariantCulture);
									var NormalizedField = double.Parse(parts[3], CultureInfo.InvariantCulture);
									var CurrentField = double.Parse(parts[4], CultureInfo.InvariantCulture);
									var Ratio = double.Parse(parts[5], CultureInfo.InvariantCulture);

									double X;
									switch (currentBlock.CurveType)
									{
										case "Beam": X = Depth; break;
										case "Inline": X = Inline; break;
										case "Crossline": X = Crossline; break;
										default: X = Math.Sign(Inline) * Math.Sqrt(Inline * Inline + Crossline * Crossline); break;
									}
									var Y = NormalizedField;

									currentBlock.Pos.Add(new double[] { X, Y });
									depths.Add(Depth);
								}
								catch (FormatException)
								{
									Console.WriteLine($"Error parsing data point: {line}");
								}
							}
							break;
					}
				}
			}

			if (currentBlock != null)
			{
				InterpolateAndSetMembers(ref currentBlock);
				currentBlock.ScanDepth = depths.Distinct().Count() == 1 ? depths.FirstOrDefault() : -1;
				blocks.Add(currentBlock); // Add last block
			}

			return blocks;
		}

	}
}
