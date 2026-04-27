using System;
using System.Collections.Generic;
using System.Linq;

namespace ProfileEvaluation
{
	/// <summary>
	/// Holds paired position (X, mm) and dose/signal (Y) arrays for a single beam profile scan,
	/// along with helpers for region queries and FWHM field-size calculation.
	/// Both arrays are required to be the same length and sorted by X ascending.
	/// </summary>
	public class XYData
	{
		/// <summary>Position values in mm, sorted ascending.</summary>
		public double[] X { get; set; } = new double[0];

		/// <summary>Dose or signal values corresponding to each X position.</summary>
		public double[] Y { get; set; } = new double[0];

		public XYData() { }

		/// <summary>Constructs from enumerable sequences; throws if lengths differ.</summary>
		public XYData(IEnumerable<double> x, IEnumerable<double> y) : this()
		{
			if (x.Count() != y.Count())
				throw new ArgumentException("X and Y arrays must have the same length.");
			X = x.ToArray();
			Y = y.ToArray();
		}

		/// <summary>Returns indices where |X[i]| ≤ <paramref name="limitToX"/>.</summary>
		public IEnumerable<int> GetIndicesWithinX(double limitToX = double.MaxValue)
		{
			return X
				.Select((v, i) => new { v, i })
				.Where(x => Math.Abs(x.v) <= limitToX)
				.Select(x => x.i);
		}

		/// <summary>Returns Y values whose corresponding X satisfies |X| ≤ <paramref name="limitToX"/>.</summary>
		public IEnumerable<double> GetYWhereXIsWithin(double limitToX = double.MaxValue)
		{
			IEnumerable<int> indices = GetIndicesWithinX(limitToX);
			return Y.Where((v, i) => indices.Contains(i));
		}

		/// <summary>
		/// Computes field size as the distance between the outermost points where Y ≥ 0.5
		/// (assumes data is normalized so that the field plateau ≈ 1.0).
		/// Throws <see cref="InvalidOperationException"/> if no values exceed 0.5.
		/// </summary>
		public double GetFieldSizeFWHM()
		{
			double dMax = Y.Max();

			// Get indices for FWHM region
			var fwhmIndices = Y
				.Select((v, i) => (v, i))
				.Where(p => p.v >= .5)
				.Select(p => p.i);

			return Math.Abs(X[fwhmIndices.First()]) + Math.Abs(X[fwhmIndices.Last()]);
		}
	}
}
