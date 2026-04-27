using System;
using System.Collections.ObjectModel;
using System.Linq;

using OxyPlot;
using OxyPlot.Series;

using VAT.Common;

namespace ProfileEvaluation
{
	/// <summary>
	/// Per-side data model (Reference or Test). Holds the collection of loaded scans,
	/// the currently selected scan, beam quality metrics (flatness, symmetry, OAR),
	/// field-centering controls, and the per-side OxyPlot preview model.
	/// Reacts to selection/centering changes by recalculating metrics and refreshing the plot.
	/// </summary>
	internal class ProfileModel : VPropertyChanged
	{
		public enum ProfileUse
		{
			None, Reference, Test
		}

		public ProfileUse ProfileType { get; set; } = ProfileUse.None;

		public ObservableCollection<DataReader.MCCData> MCCData { get; set; } = new ObservableCollection<DataReader.MCCData>();
		public ObservableValue<DataReader.MCCData> SelectedData { get; } = new ObservableValue<DataReader.MCCData>();

		public ObservableValue<double> FlatnessVarian { get; } = new ObservableValue<double>();
		public ObservableValue<double> FlatnessElekta { get; } = new ObservableValue<double>();
		public ObservableValue<double> SymmetryVarian { get; } = new ObservableValue<double>();
		public ObservableValue<double> SymmetryElekta { get; } = new ObservableValue<double>();
		public ObservableValue<double> OffAxisRatio { get; } = new ObservableValue<double>();

		public ObservableValue<bool> CenterField { get; } = new ObservableValue<bool>() { Value = false };
		public ObservableValue<double> CenterFieldAtFWOf { get; } = new ObservableValue<double>() { Value = 50 };

		// Create a new PlotModel
		private PlotModel plotModel = new PlotModel
		{
			Title = "",
			TitleFontSize = 10
		};
		public PlotModel PlotModel
		{
			get => plotModel;
			private set => SetProperty(ref plotModel, value);
		}

		public ProfileModel()
		{

			ViewModel.InitializePlot(PlotModel);
			var ax = PlotModel.Axes.FirstOrDefault(x => x.Key == "GammaAxis");
			if (ax != null) ax.IsAxisVisible = false;

			
			SelectedData.OnChanged += () =>
			{
				CalculateFieldParameters();
				PlotMCCData(clearFirst: true);
			};
			CenterField.OnChanged += () =>
			{
				CalculateFieldParameters();
				PlotMCCData(clearFirst: true);
			};
			CenterFieldAtFWOf.OnChanged += () =>
			{
				CalculateFieldParameters();
				PlotMCCData(clearFirst: true);
			};
		}

		//public void ReadMccFile(string filename)

		public void ReadMccFile(string fileName)
		{
			foreach (var data in DataReader.ReadMCCData(fileName))
				MCCData.Add(data);
		}

		public void ReadDicomFile(string fileName)
		{
			foreach (var data in DataReader.ParseDicomToMcc(fileName))
				MCCData.Add(data);
		}

		public void ReadOmniProCsvFile(string fileName)
		{
			foreach (var data in DataReader.ParseOmniProCsvToMcc(fileName))
				MCCData.Add(data);
		}

		/// <summary>
		/// Recalculates flatness (Varian and Elekta), symmetry (Varian and Elekta), and
		/// off-axis ratio for the currently selected scan, optionally after field centering.
		/// Clears all metrics to 0 when no scan is selected.
		/// </summary>
		public void CalculateFieldParameters()
		{
			if (SelectedData.Value != null)
			{
				var xy = new XYData(SelectedData.Value.GetXData(), SelectedData.Value.GetYData());

				if (CenterField.Value)
					xy = ProfileAnalysis.ShiftAndTruncateProfile(xy, CenterFieldAtFWOf.Value / 100);

				var halfFieldSize = xy.GetFieldSizeFWHM() / 2;
				var (VarianSymmetry, VarianFlatness, ElektaSymmetry, ElektaFlatness) = ProfileAnalysis.CalculateFlatnessAndSymmetry(xy, 0.8 * halfFieldSize);

				FlatnessElekta.Value = Math.Round(100 * ElektaFlatness, ElektaFlatness >= .1 ? 1 : 2);
				FlatnessVarian.Value = Math.Round(100 * VarianFlatness, VarianFlatness >= .1 ? 1 : 2);
				SymmetryElekta.Value = Math.Round(100 * ElektaSymmetry, ElektaSymmetry >= .1 ? 1 : 2);
				SymmetryVarian.Value = Math.Round(100 * VarianSymmetry, VarianSymmetry >= .1 ? 1 : 2);

				var offAxisRatio = ProfileAnalysis.CalculateOffAxisRatio(xy);
				OffAxisRatio.Value = Math.Round(offAxisRatio, 2);
			}
			else
			{
				FlatnessElekta.Value = 0;
				FlatnessVarian.Value = 0;
				SymmetryElekta.Value = 0;
				SymmetryVarian.Value = 0;
				OffAxisRatio.Value = 0;
			}
		}

		public void PlotMCCData(bool clearFirst = false)
		{
			if (clearFirst)
				PlotModel.Series.Clear();

			if (SelectedData.Value != null)
			{

				LineSeries lineSeries;
				switch (ProfileType)
				{
					case ProfileUse.Reference:
						lineSeries = ViewModel.LineSeriesDefault_Reference;
						break;
					case ProfileUse.Test:
						lineSeries = ViewModel.LineSeriesDefault_Test;
						break;
					case ProfileUse.None:
					default:
						lineSeries = ViewModel.LineSeriesDefault;
						break;
				}
				var xdata = SelectedData.Value.GetXData();
				var ydata = SelectedData.Value.GetYData();
				for (int i = 0; i < xdata.Count && i < ydata.Count; i++)
					lineSeries.Points.Add(new DataPoint(xdata[i], ydata[i]));
				PlotModel.Series.Add(lineSeries);
			}

			UpdatePlot();
		}

		public void UpdatePlot(bool updateData = false)
		{
			PlotModel.ResetAllAxes();

			if (MCCData.Count == 0 || SelectedData is null) return;

			PlotModel.DefaultXAxis.Minimum = SelectedData.Value
				.GetXData()
				.Where(y => !(double.IsInfinity(y) || double.IsNaN(y)))
				.Min();
			PlotModel.DefaultXAxis.Maximum = SelectedData.Value
				.GetXData()
				.Where(y => !(double.IsInfinity(y) || double.IsNaN(y)))
				.Max();

			PlotModel.DefaultYAxis.Minimum = 0;
			PlotModel.DefaultYAxis.Maximum = SelectedData.Value
				.GetYData()
				.Where(y => !(double.IsInfinity(y) || double.IsNaN(y)))
				.Max() * 1.05;
			if (PlotModel.DefaultYAxis.Maximum > 2)
				PlotModel.DefaultYAxis.Maximum = 2;

			PlotModel.InvalidatePlot(updateData);
		}
	}
}
