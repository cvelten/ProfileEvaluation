using System.Windows;

namespace ProfileEvaluation
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App() : base()
		{
			// Force assembly loading
			var _ = new OxyPlot.Wpf.PlotView();
		}
	}
}
