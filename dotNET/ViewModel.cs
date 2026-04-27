using System;
using System.Linq;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

using VAT.Common;

namespace ProfileEvaluation
{
	/// <summary>
	/// Main application view model. Owns gamma comparison settings, result metrics,
	/// the two side-by-side <see cref="ProfileModel"/> instances (reference and test),
	/// and the combined comparison <see cref="OxyPlot.PlotModel"/>.
	/// Drives <see cref="CompareData"/> which normalizes both profiles, computes gamma,
	/// and updates the plot with all series.
	/// </summary>
	internal class ViewModel : ViewModelBase
	{
		#region Models
		private ProfileModel leftModel = new ProfileModel() { ProfileType = ProfileModel.ProfileUse.Reference };
		public ProfileModel LeftModel
		{
			get => leftModel;
			private set => SetProperty(ref leftModel, value);
		}

		private ProfileModel rightModel = new ProfileModel() { ProfileType = ProfileModel.ProfileUse.Test };
		public ProfileModel RightModel
		{
			get => rightModel;
			private set => SetProperty(ref rightModel, value);
		}

		private PlotModel plotModel = new PlotModel()
		{
			IsLegendVisible = true,
		};
		public PlotModel PlotModel
		{
			get => plotModel;
			private set => SetProperty(ref plotModel, value);
		}
		#endregion

		#region Properties
		private double gammaDTA = 1;
		public double GammaDTA
		{
			get => gammaDTA;
			set
			{
				if (value > 0 && SetProperty(ref gammaDTA, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private double gammaDD = 2;
		public double GammaDD
		{
			get => gammaDD;
			set
			{
				if (value > 0 && SetProperty(ref gammaDD, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private bool gammaNormalizeCAX = true;
		public bool GammaNormalizeCAX
		{
			get => gammaNormalizeCAX;
			set
			{
				if (SetProperty(ref gammaNormalizeCAX, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private bool gammaNormalizeMax = false;
		public bool GammaNormalizeMax
		{
			get => gammaNormalizeMax;
			set
			{
				if (SetProperty(ref gammaNormalizeMax, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private bool gammaGlobalComparison = false;
		public bool GammaGlobalComparison
		{
			get => gammaGlobalComparison;
			set
			{
				if (SetProperty(ref gammaGlobalComparison, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private bool gammaRestrictToFieldSize = true;
		public bool GammaRestrictToFieldSize
		{
			get => gammaRestrictToFieldSize;
			set
			{
				if (SetProperty(ref gammaRestrictToFieldSize, value))
					ComparisonInvalidated.Value = true;
			}
		}
		private double gammaRestrictToFieldSizePercent = 80;
		public double GammaRestrictToFieldSizePercent
		{
			get => gammaRestrictToFieldSizePercent;
			set
			{
				if (value > 0 && value <= 1000 && SetProperty(ref gammaRestrictToFieldSizePercent, value))
					ComparisonInvalidated.Value = true;
			}
		}

		private double gammaResultFractionLessThanOne = double.NaN;
		public double GammaResultFractionLessThanOne
		{
			get => gammaResultFractionLessThanOne;
			set => SetProperty(ref gammaResultFractionLessThanOne, value);
		}
		private double gammaResultAverage = double.NaN;
		public double GammaResultAverage
		{
			get => gammaResultAverage;
			set => SetProperty(ref gammaResultAverage, value);
		}
		private double gammaResultMax = double.NaN;
		public double GammaResultMax
		{
			get => gammaResultMax;
			set => SetProperty(ref gammaResultMax, value);
		}

		public ObservableValue<bool> ComparisonInvalidated { get; } = new ObservableValue<bool> { Value= true };
		#endregion


		public ViewModel()
		{

			LeftModel.SelectedData.OnChanged += () => ComparisonInvalidated.Value = true;
			RightModel.SelectedData.OnChanged += () => ComparisonInvalidated.Value = true;

			LeftModel.CenterField.OnChanged += () => ComparisonInvalidated.Value = true;
			LeftModel.CenterFieldAtFWOf.OnChanged += () => ComparisonInvalidated.Value = true;
			RightModel.CenterField.OnChanged += () => ComparisonInvalidated.Value = true;
			RightModel.CenterFieldAtFWOf.OnChanged += () => ComparisonInvalidated.Value = true;
		}

		public override void Initialize()
		{
			base.Initialize();

			InitializePlot(PlotModel);
			var ax = PlotModel.Axes.FirstOrDefault(x => x.Key == "ProfileAxis");
			if (ax != null) ax.MajorGridlineStyle = LineStyle.None;
		}

		/// <summary>
		/// Normalizes reference and test profiles per the current normalization mode,
		/// optionally centers and restricts them to a percentage of the FWHM field size,
		/// computes the gamma index array, stores summary metrics, and rebuilds all plot series.
		/// Throws <see cref="ArgumentOutOfRangeException"/> if either profile selection is empty.
		/// </summary>
		public void CompareData()
		{
			if (LeftModel.SelectedData.Value is null)
				throw new ArgumentOutOfRangeException("Exactly one scan must be selected as reference (left view)!");
			if (RightModel.SelectedData.Value is null)
				throw new ArgumentOutOfRangeException("Exactly one scan must be selected as test (right view)!");

			var xyReference = new XYData(LeftModel.SelectedData.Value.GetXData(), LeftModel.SelectedData.Value.GetYData());
			var xyTest = new XYData(RightModel.SelectedData.Value.GetXData(), RightModel.SelectedData.Value.GetYData());

			if (LeftModel.CenterField.Value)
				xyReference = ProfileAnalysis.ShiftAndTruncateProfile(xyReference, LeftModel.CenterFieldAtFWOf.Value / 100);
			if (RightModel.CenterField.Value)
				xyTest = ProfileAnalysis.ShiftAndTruncateProfile(xyTest, RightModel.CenterFieldAtFWOf.Value / 100);

			if (GammaNormalizeCAX)
			{
				int idx = xyReference.X.FindIndexOfMin(absolute: true);
				xyReference.Y = xyReference.Y.Select(y => y / xyReference.Y[idx]).ToArray();
				idx = xyTest.X.FindIndexOfMin(absolute: true);
				xyTest.Y = xyTest.Y.Select(y => y / xyTest.Y[idx]).ToArray();
			}
			else if (GammaNormalizeMax)
			{
				double Dmax = xyReference.Y.Max();
				xyReference.Y = xyReference.Y.Select(y => y / Dmax).ToArray();
				Dmax = xyTest.Y.Max();
				xyTest.Y = xyTest.Y.Select(y => y / Dmax).ToArray();
			}
			else { } // no normalization

			double limitToX = double.MaxValue;
			if (GammaRestrictToFieldSize)
			{
				//double fieldSize = reference.GetFieldSizeInScanDirection();
				var fieldSize = xyReference.GetFieldSizeFWHM();
				limitToX = fieldSize / 2 * GammaRestrictToFieldSizePercent / 100;
			}

			double[] gamma = ProfileAnalysis.ComputeGamma(xyReference, xyTest, GammaDTA, GammaDD / 100,
				globalGamma: GammaGlobalComparison, limitToX: limitToX);

			// Set results
			GammaResultMax = Math.Round(gamma.Where(x => !double.IsNaN(x)).Max(), 2);
			GammaResultAverage = Math.Round(gamma.Where(x => !double.IsNaN(x)).Average(), 2);
			GammaResultFractionLessThanOne = Math.Round(100 * gamma.Where(x => !double.IsNaN(x) && x < 1).Count() / ((double)gamma.Where(x => !double.IsNaN(x)).Count()), 2);


			// Plotting
			{
				PlotModel.Series.Clear();

				// Analysis Limit
				if (GammaRestrictToFieldSize)
				{
					var lineSeries = LineSeriesDefault_AxLine;
					lineSeries.Title = "Analysis Region";
					lineSeries.Points.Add(new DataPoint(-limitToX, 0));
					lineSeries.Points.Add(new DataPoint(-limitToX, xyReference.X.Max()));
					PlotModel.Series.Add(lineSeries);

					lineSeries = LineSeriesDefault_AxLine;
					lineSeries.Points.Add(new DataPoint(limitToX, 0));
					lineSeries.Points.Add(new DataPoint(limitToX, xyReference.X.Max()));
					PlotModel.Series.Add(lineSeries);
				}


				// Gamma
				{
					var lineSeries = LineSeriesDefault_AxLine;
					lineSeries.YAxisKey = "GammaAxis";
					lineSeries.LineStyle = LineStyle.LongDash;
					lineSeries.Color = OxyColors.SaddleBrown;
					lineSeries.Points.Add(new DataPoint(xyReference.X.Min(), 1));
					lineSeries.Points.Add(new DataPoint(xyReference.X.Max(), 1));
					PlotModel.Series.Add(lineSeries);

					lineSeries = LineSeriesDefault_Gamma(GammaDD, GammaDTA);
					lineSeries.YAxisKey = "GammaAxis";
					// Add data points from pos (using the first dimension) and DataCAX
					for (int i = 0; i < xyReference.X.Length && i < gamma.Length; ++i)
					{
						lineSeries.Points.Add(new DataPoint(xyReference.X[i], gamma[i]));
					}
					PlotModel.Series.Add(lineSeries);
				}

				// Add profiles, normalized to max gamma
				{
					var lineSeries = LineSeriesDefault_Reference;
					//var norm = gamma.Max() / (xyReference.Y.Max() + xyTest.Y.Max()) * 2;
					var norm = 1;
					for (int i = 0; i < xyReference.X.Length && i < xyReference.Y.Length; ++i)
					{
						lineSeries.Points.Add(new DataPoint(xyReference.X[i], xyReference.Y[i] * norm));
					}
					PlotModel.Series.Add(lineSeries);
				}
				{
					var lineSeries = LineSeriesDefault_Test;
					//var norm = gamma.Max() / (xyReference.Y.Max() + xyTest.Y.Max()) * 2;
					var norm = 1;
					for (int i = 0; i < xyTest.X.Length && i < xyTest.Y.Length; ++i)
					{
						lineSeries.Points.Add(new DataPoint(xyTest.X[i], xyTest.Y[i] * norm));
					}
					PlotModel.Series.Add(lineSeries);
				}

				PlotModel.ResetAllAxes();
				PlotModel.DefaultXAxis.Minimum = xyReference.X.Min();
				PlotModel.DefaultXAxis.Maximum = xyReference.X.Max();
				foreach (var ax in PlotModel.Axes)
				{
					if (ax.Key == "GammaAxis")
					{
						ax.Minimum = 0;
						ax.Maximum = gamma.Where(g => g < double.MaxValue).Max() * 1.05;

						if (ax.Maximum < 1.05)
							ax.Maximum = 1.05;
					}
					else if (ax.IsVertical())
					{
						ax.Minimum = 0;
						ax.Maximum = Math.Max(xyReference.Y.Max(), xyTest.Y.Max()) * 1.05;
					}
				}
				PlotModel.InvalidatePlot(false);
			}
		}

		/// <summary>
		/// Configures a <see cref="PlotModel"/> with the standard three axes:
		/// X = position [mm], Y_left = normalized profile [a.u.], Y_right = gamma index.
		/// Called both on the main comparison plot and on each <see cref="ProfileModel"/>'s
		/// individual preview plot (where the gamma axis is then hidden).
		/// </summary>
		public static void InitializePlot(PlotModel model)
		{
			var yAxis = new LinearAxis()
			{
				Title = "norm. profile [a.u.]",
				Key = "ProfileAxis",
				FontSize = 10,
				Position = AxisPosition.Left,
				AbsoluteMinimum = 0,
				Minimum = 0,
				Maximum = 1,
				//MinimumMajorStep = 10,
				//MinimumMinorStep = 5,
				MinorGridlineStyle = LineStyle.None,
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
			};
			var y2Axis = new LinearAxis()
			{
				Title = "gamma",
				Key = "GammaAxis",
				FontSize = 10,
				Position = AxisPosition.Right,
				AbsoluteMinimum = 0,
				Minimum = 0,
				Maximum = 2,
				MinorGridlineStyle = LineStyle.None,
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
			};
			var xAxis = new LinearAxis()
			{
				Title = "position [mm]",
				FontSize = 10,
				Position = AxisPosition.Bottom,
				Minimum = -200,
				Maximum = 200,
				AbsoluteMinimum = -400,
				AbsoluteMaximum = +400,
				MinimumMajorStep = 10,
				MinimumMinorStep = 5,
				MinorGridlineStyle = LineStyle.None,
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
			};

			model.Padding = new OxyThickness(0);

			model.Axes.Clear();
			model.Axes.Add(xAxis);
			model.Axes.Add(yAxis);
			model.Axes.Add(y2Axis);

			model.Legends.Add(new Legend
			{
				LegendPosition = LegendPosition.RightTop,
				LegendBorder = OxyColors.DarkGray,
				LegendBackground = OxyColors.WhiteSmoke,
				LegendPadding = 3,
				FontSize = 7
			});
		}

		public static LineSeries LineSeriesDefault_Gamma(double GammaDD, double GammaDTA) => new LineSeries
		{
			Title = $"Gamma({GammaDD}%,{GammaDTA}mm)",
			StrokeThickness = 2,
			Color = OxyColors.ForestGreen,
			MarkerType = MarkerType.None,
			MarkerSize = 3
		};

		public static LineSeries LineSeriesDefault_AxLine => new LineSeries
		{
			StrokeThickness = 2,
			Color = OxyColors.SteelBlue,
			MarkerType = MarkerType.None,
			LineStyle = LineStyle.LongDash,
		};

		public static LineSeries LineSeriesDefault_Reference => new LineSeries
		{
			Title = "Reference",
			StrokeThickness = 3,
			Color = OxyColors.Black,
			LineStyle = LineStyle.Solid,
			MarkerType = MarkerType.None,
		};

		public static LineSeries LineSeriesDefault_Test => new LineSeries
		{
			Title = "Test",
			StrokeThickness = 3,
			Color = OxyColors.Red,
			LineStyle = LineStyle.DashDashDot,
			MarkerType = MarkerType.None,
		};

		public static LineSeries LineSeriesDefault => new LineSeries
		{
			StrokeThickness = 3,
			LineStyle = LineStyle.Solid,
			MarkerType = MarkerType.None,
		};
	}
}
