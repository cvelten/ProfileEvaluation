using System;
using System.Linq;

namespace ProfileEvaluation
{
	/// <summary>
	/// Static methods for beam profile analysis: gamma index computation, field centering,
	/// flatness/symmetry metrics (Varian and Elekta conventions), and off-axis ratio.
	/// </summary>
	public class ProfileAnalysis
	{
		/// <summary>
		/// Centers a profile on its field midpoint and trims it symmetrically about that center.
		/// The midpoint is the average of the left and right positions where Y first exceeds
		/// <paramref name="fullWidthAt"/> × Y_max. After shifting, the profile is truncated to
		/// the shortest symmetric half so both sides have equal extent.
		/// </summary>
		/// <param name="profile">Input profile (X in mm, Y in dose units).</param>
		/// <param name="fullWidthAt">Fraction of max dose defining the field edge (0–1). Default 0.5 = FWHM.</param>
		/// <returns>New <see cref="XYData"/> centered at 0 with symmetric extent.</returns>
		public static XYData ShiftAndTruncateProfile(XYData profile, double fullWidthAt = 0.5)
		{
			if (fullWidthAt < 0 || fullWidthAt > 1)
				throw new ArgumentOutOfRangeException($"0 <= fullWidthAt <= 1 is required but fullWidthAt={fullWidthAt}");

			// Find the maximum value of Y and calculate half maximum
			double maxY = profile.Y.Max();
			double percentOfMax = maxY * fullWidthAt;

			// Find indices where Y crosses the half maximum
			int leftIndex = Array.FindIndex(profile.Y, y => y >= percentOfMax);
			int rightIndex = Array.FindLastIndex(profile.Y, y => y >= percentOfMax);

			// Ensure valid indices for FWHM
			if (leftIndex == -1 || rightIndex == -1 || leftIndex >= rightIndex)
			{
				throw new InvalidOperationException($"Unable to determine FW@{fullWidthAt:.2f} from the provided data.");
			}

			// Calculate the center of the FWHM range
			double fwCenterX = (profile.X[leftIndex] + profile.X[rightIndex]) / 2.0;

			// Shift X values so that the FWHM center is at 0
			double[] shiftedX = profile.X.Select(x => x - fwCenterX).ToArray();


			// Find the symmetric range by determining the shorter distance to the edges
			int symmetricLength = Math.Min(leftIndex, profile.X.Length - rightIndex - 1);

			// Determine the start and end indices for symmetry
			int symmetricStart = leftIndex - symmetricLength;
			int symmetricEnd = rightIndex + symmetricLength;

			// Create new symmetric arrays
			double[] symmetricX = shiftedX.Skip(symmetricStart).Take(symmetricEnd - symmetricStart + 1).ToArray();
			double[] symmetricY = profile.Y.Skip(symmetricStart).Take(symmetricEnd - symmetricStart + 1).ToArray();

			// Return the updated profile
			return new XYData(symmetricX, symmetricY);
		}

		/// <summary>
		/// Computes flatness and symmetry for both Varian and Elekta conventions within the
		/// region ±<paramref name="limitToX"/> mm from the central axis.
		/// <list type="bullet">
		///   <item>Varian flatness: (Dmax − Dmin) / (Dmax + Dmin)</item>
		///   <item>Varian symmetry: max |D(−x) − D(x)| / D_CAX over the inner half</item>
		///   <item>Elekta flatness: Dmax / Dmin</item>
		///   <item>Elekta symmetry: max(D(−x)/D(x), D(x)/D(−x)) over the inner half</item>
		/// </list>
		/// </summary>
		/// <param name="data">Profile data (should be normalized or at least have consistent units).</param>
		/// <param name="limitToX">Half-width in mm of the analysis region. Defaults to full profile.</param>
		public static (double VarianSymmetry, double VarianFlatness, double ElektaSymmetry, double ElektaFlatness)
			CalculateFlatnessAndSymmetry(XYData data, double limitToX = double.MaxValue)
		{
			var validIndices = data.GetIndicesWithinX(limitToX);

			double[] fwhmX = data.X.Where((v, i) => i >= validIndices.First() && i <= validIndices.Last()).ToArray();
			double[] fwhmY = data.Y.Where((v, i) => i >= validIndices.First() && i <= validIndices.Last()).ToArray();

			var caxIdx = fwhmX.FindIndexOfMin(absolute: true);
			var Dcax = fwhmY[caxIdx];
			var Dmin = fwhmY.Min();
			var Dmax = fwhmY.Max();

			//
			// VARIAN

			double VarianSymmetry = fwhmX
				.Take(fwhmX.Length / 2)
				.Select((v, i) =>
				{
					double lVal = fwhmY[i];
					double rVal = fwhmY[fwhmY.Length - 1 - i];
					return Math.Abs(lVal - rVal) / Dcax;
				})
				.Max();

			double VarianFlatness = (Dmax - Dmin) / (Dmax + Dmin);

			//
			// ELEKTA

			double ElektaSymmetry = fwhmX
				.Take(fwhmX.Length / 2)
				.Select((v, i) =>
				{
					double lVal = fwhmY[i];
					double rVal = fwhmY[fwhmY.Length - 1 - i];
					double lr = lVal / rVal;
					double rl = rVal / lVal;
					return Math.Max(lr, rl);
				})
				.Max();

			double ElektaFlatness = Dmax / Dmin;

			return (VarianSymmetry, VarianFlatness, ElektaSymmetry, ElektaFlatness);
		}

		/// <summary>
		/// Calculates off-axis ratio (OAR) at a distance derived from the FWHM field size:
		/// 60% of half-field for small fields (&lt;100mm), 80% for larger fields.
		/// OAR = 2·D_CAX / (D_minus + D_plus).
		/// </summary>
		public static double CalculateOffAxisRatio(XYData data)
		{
			var fieldSize = data.GetFieldSizeFWHM();
			var percentageOfFieldSize = fieldSize < 100 ? 0.6 : 0.8;
			return CalculateOffAxisRatio(data, percentageOfFieldSize * fieldSize / 2);
		}

		/// <summary>
		/// Calculates off-axis ratio at a specific distance from the central axis.
		/// OAR = 2·D_CAX / (D(−x) + D(+x)) where x = <paramref name="atDistanceFromCax"/>.
		/// </summary>
		public static double CalculateOffAxisRatio(XYData data, double atDistanceFromCax)
		{
			var validIndices = data.GetIndicesWithinX(atDistanceFromCax);
			double[] fwhmX = data.X.Where((v, i) => i >= validIndices.First() && i <= validIndices.Last()).ToArray();
			double[] fwhmY = data.Y.Where((v, i) => i >= validIndices.First() && i <= validIndices.Last()).ToArray();

			var Dcax = fwhmY[fwhmX.FindIndexOfMin(absolute: true)];
			var Dminus = fwhmY[fwhmX.FindIndexOfMin()];
			var Dplus = fwhmY[fwhmX.FindIndexOfMax()];

			return 2 * Dcax / (Dminus + Dplus);
		}

		/// <summary>
		/// Computes the gamma index for each reference point against a test profile using the
		/// standard DTA/DD formulation: γ = min_j √[(Δx/DTA)² + (ΔD/DD)²].
		/// Points outside ±<paramref name="limitToX"/> are set to <see cref="double.NaN"/>.
		/// </summary>
		/// <param name="reference">Reference profile (already normalized if needed).</param>
		/// <param name="test">Test profile (already normalized to the same scale).</param>
		/// <param name="dta">Distance-to-agreement criterion in mm.</param>
		/// <param name="dd">Dose-difference criterion as a fraction (e.g. 0.02 for 2%).</param>
		/// <param name="globalGamma">
		///   If true, dose difference is normalized to the global maximum of the reference.
		///   If false, local gamma (normalized to each reference point's dose) is used.
		/// </param>
		/// <param name="limitToX">Half-width in mm of the analysis region.</param>
		/// <returns>Array of gamma values aligned with <paramref name="reference"/>.X. NaN where outside region.</returns>
		public static double[] ComputeGamma(XYData reference, XYData test, double dta, double dd,
			bool globalGamma = true, double limitToX = double.MaxValue)
		{
			int refLength = reference.X.Length;
			int testLength = test.X.Length;
			double[] gammaValues = new double[refLength];

			double globalMaximum = 0;
			if (globalGamma)
			{
				for (var i = 0; i < refLength; ++i)
					if (Math.Abs(reference.X[i]) <= limitToX && reference.Y[i] > globalMaximum)
						globalMaximum = reference.Y[i];
			}

			for (int i = 0; i < refLength; i++)
			{
				if (Math.Abs(reference.X[i]) <= limitToX)
				{
					double minGamma = double.MaxValue;

					for (int j = 0; j < testLength; j++)
					{
						double dtaTerm = (reference.X[i] - test.X[j]) / dta;
						double ddTerm = (reference.Y[i] - test.Y[j]) / dd;
						ddTerm /= globalGamma ? globalMaximum : reference.Y[i];

						double gamma = Math.Sqrt(dtaTerm * dtaTerm + ddTerm * ddTerm);

						if (gamma < minGamma)
						{
							minGamma = gamma;
						}
					}

					gammaValues[i] = minGamma; // Store the minimum gamma value for this reference point
				}
				else
					gammaValues[i] = double.NaN;
			}

			return gammaValues;
		}
	}
}
