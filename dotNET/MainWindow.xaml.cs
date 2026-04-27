using Microsoft.Win32;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ProfileEvaluation
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	internal partial class MainWindow : VAT.Common.WindowBase<ViewModel>
	{
		readonly OpenFileDialog openFileDialogLeft = new OpenFileDialog()
		{
			Filter = "MCC, OmniPro CSV, or DICOM Files (*.mcc,*.csv,*.dcm)|*.mcc;*.csv;*.dcm|MCC Files (*.mcc)|*.mcc|iba OmniPro CSV Files (*.csv)|*.csv|DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*",
			Multiselect = true,
			Title = "Select Files",
		};
		readonly OpenFileDialog openFileDialogRight = new OpenFileDialog()
		{
			Filter = "MCC, OmniPro CSV, or DICOM Files (*.mcc,*.csv,*.dcm)|*.mcc;*.csv;*.dcm|MCC Files (*.mcc)|*.mcc|iba OmniPro CSV Files (*.csv)|*.csv|DICOM Files (*.dcm)|*.dcm|All Files (*.*)|*.*",
			Multiselect = true,
			Title = "Select Files"
		};

		public MainWindow() : this(new ViewModel())
		{ }

		public MainWindow(ViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;
		}

		private IEnumerable<(string, string)> TryToReadFilesWith(IEnumerable<string> fileNames, Action<string> readerFunction)
		{
			var filesWithIssues = new List<(string fileName, string exceptionString)>();
			foreach (var file in fileNames)
			{
				try
				{
					readerFunction(file);
				}
				catch (Exception ex)
				{
					filesWithIssues.Add((file, ex.Message));
				}
			}
			return filesWithIssues;
		}

		private void Button_AddFiles_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button)
			{
				OpenFileDialog dialog = null;
				ProfileModel model = null;
				if (button == Button_AddFiles_Left)
				{
					dialog = openFileDialogLeft;
					model = ViewModel?.LeftModel;
				}
				else if (button == Button_AddFiles_Right)
				{
					dialog = openFileDialogRight;
					model = ViewModel?.RightModel;
				}
				else
					return;
				if (dialog is null || model is null)
					return;

				if (dialog.ShowDialog() ?? false)
				{
					var mccResult = TryToReadFilesWith(
						dialog.FileNames.Where(x => x.EndsWith(".mcc")),
						model.ReadMccFile);
					var dicomResult = TryToReadFilesWith(
						dialog.FileNames.Where(x => x.EndsWith(".dcm")),
						model.ReadDicomFile);
					var omniproResult = TryToReadFilesWith(
						dialog.FileNames.Where(x => x.EndsWith(".csv")),
						model.ReadOmniProCsvFile);

					if (mccResult?.Count() > 0)
						MessageBox.Show("Issues reading MCC file(s):\n" + string.Join("\n", mccResult),
							"Issue reading MCC", MessageBoxButton.OK, MessageBoxImage.Error);
					if (dicomResult?.Count() > 0)
						MessageBox.Show("Issues reading DICOM file(s):\n" + string.Join("\n", dicomResult),
							"Issue reading DICOM", MessageBoxButton.OK, MessageBoxImage.Error);
					if (omniproResult?.Count() > 0)
						MessageBox.Show("Issues reading OmniPro CSV file(s):\n" + string.Join("\n", omniproResult),
							"Issue reading OmniPro CSV", MessageBoxButton.OK, MessageBoxImage.Error);

					dialog.InitialDirectory = Path.GetDirectoryName(dialog.FileNames.FirstOrDefault() ?? Assembly.GetExecutingAssembly().Location);
				}
				else
				{
					dialog.InitialDirectory = null;
				}
			}
		}

		private void Button_Clear_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button)
			{
				if (button == Button_ClearFiles_Left)
					ViewModel?.LeftModel.MCCData.Clear();
				else if (button == Button_ClearFiles_Right)
					ViewModel?.RightModel.MCCData.Clear();
			}
		}

		private void Button_Compare_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ViewModel?.CompareData();
				if (ViewModel != null)
					ViewModel.ComparisonInvalidated.Value = false;
			}
			catch (ArgumentOutOfRangeException ex)
			{
				MessageBox.Show(ex.Message, ex.GetType().Name,
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}


		public void PrintVisualToPdf(Grid grid)
		{
			var pd = new PrintDialog();

			// Set print queue to Microsoft Print to PDF if available
			foreach (PrintQueue pq in new LocalPrintServer().GetPrintQueues())
			{
				if (pq.Name.Contains("Microsoft Print to PDF"))
				{
					pd.PrintQueue = pq;
					break;
				}
			}

			// US LTTR 215.9mm x 279.4mm
			// A4	   210 mm x 297 mm

			// Show print dialog to user
			if (pd.ShowDialog() == true)
			{
				grid.Dispatcher.Invoke(() =>
				{
					grid.UpdateLayout();
				});

				var document = new FlowDocument()
				{
					FontFamily = new FontFamily("Arial"),
					FontSize = 10,

					//PageHeight = pd.PrintableAreaHeight > 297 ? 297 : pd.PrintableAreaHeight,
					//PageWidth = pd.PrintableAreaWidth > 215.9 ? 215.9 : pd.PrintableAreaWidth,
					PageHeight = pd.PrintableAreaHeight,
					PageWidth = pd.PrintableAreaWidth,
					PagePadding = new Thickness(25),
					ColumnGap = 0,
					ColumnWidth = pd.PrintableAreaWidth,
				};

				document.Blocks.Add(new Paragraph(new Run("Profile Comparison"))
				{
					FontSize = 17,
					FontWeight = FontWeights.Bold,
					Margin = new Thickness(10),
					TextAlignment = TextAlignment.Center,
				});

				// Date and Filenames etc.
				{
					var sp = new StackPanel
					{
						Margin = new Thickness(10),
						Orientation = Orientation.Vertical
					};

					// Info
					{
						StackPanel _sp = new StackPanel() { Orientation = Orientation.Horizontal };
						_sp.Children.Add(new TextBlock(new Run("Date/Time:"))
						{
							MinWidth = 100,
							MaxWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Right,
						});
						_sp.Children.Add(new TextBlock(new Run(DateTime.Now.ToString("yyyy-MM-dd [hh:mmtt]")))
						{
							MinWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Stretch,
						});
						sp.Children.Add(_sp);
						sp.Children.Add(new Separator() { Margin = new Thickness(20), Background = Brushes.Transparent });
					}

					// Reference
					{
						sp.Children.Add(new TextBlock(new Run("Reference Profile")
						{
							FontSize = 14,
							FontWeight = FontWeights.Bold,
						}));

						StackPanel _sp = new StackPanel() { Orientation = Orientation.Horizontal };
						_sp.Children.Add(new TextBlock(new Run("Display Name:"))
						{
							MinWidth = 100,
							MaxWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Right,
						});
						_sp.Children.Add(new TextBlock(new Run(ViewModel?.LeftModel.SelectedData.Value?.GetInfoString ?? "n/a"))
						{
							MinWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Stretch,
						});
						sp.Children.Add(_sp);

						_sp = new StackPanel() { Orientation = Orientation.Horizontal };
						_sp.Children.Add(new TextBlock(new Run("Filename:"))
						{
							MinWidth = 100,
							MaxWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Right,
						});
						_sp.Children.Add(new TextBlock(new Run(ViewModel?.LeftModel.SelectedData.Value?.FileName ?? "n/a"))
						{
							MinWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Stretch,
						});
						sp.Children.Add(_sp);

						sp.Children.Add(new Separator() { Margin = new Thickness(20), Background = Brushes.Transparent });
					}

					// Test
					{
						sp.Children.Add(new TextBlock(new Run("Test Profile")
						{
							FontSize = 14,
							FontWeight = FontWeights.Bold,
						}));

						StackPanel _sp = new StackPanel() { Orientation = Orientation.Horizontal };
						_sp.Children.Add(new TextBlock(new Run("Display Name:"))
						{
							MinWidth = 100,
							MaxWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Right,
						});
						_sp.Children.Add(new TextBlock(new Run(ViewModel?.RightModel.SelectedData.Value?.GetInfoString ?? "n/a"))
						{
							MinWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Stretch,
						});
						sp.Children.Add(_sp);

						_sp = new StackPanel() { Orientation = Orientation.Horizontal };
						_sp.Children.Add(new TextBlock(new Run("Filename:"))
						{
							MinWidth = 100,
							MaxWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Right,
						});
						_sp.Children.Add(new TextBlock(new Run(ViewModel?.RightModel.SelectedData.Value?.FileName ?? "n/a"))
						{
							MinWidth = 100,
							Margin = new Thickness(5),
							HorizontalAlignment = HorizontalAlignment.Stretch,
						});
						sp.Children.Add(_sp);

						sp.Children.Add(new Separator() { Margin = new Thickness(20) });
					}

					document.Blocks.Add(new BlockUIContainer()
					{
						Child = sp
					});
				}

				{
					var sp = new StackPanel
					{
						Orientation = Orientation.Horizontal,
						HorizontalAlignment = HorizontalAlignment.Left,
						Margin = new Thickness(20, 5, 20, 20),
					};

					var settingsAndResults = (Grid)XamlReader.Parse(XamlWriter.Save(ComparisonSettingsAndResultsPanel));
					sp.Children.Add(settingsAndResults);
					sp.Children.Add(new Separator() { Margin = new Thickness(10) });

					var spFieldLt = new StackPanel { Orientation = Orientation.Vertical };
					{
						spFieldLt.Children.Add(new TextBlock(new Run("Reference Profile"))
						{
							FontWeight = FontWeights.Bold,
							Margin = new Thickness(3, 0, 3, 10)
						});
						var fieldResults = (Grid)XamlReader.Parse(XamlWriter.Save(FieldAnalysisReferenceLeft));
						spFieldLt.Children.Add(fieldResults);
					}
					sp.Children.Add(spFieldLt);
					sp.Children.Add(new Separator() { Margin = new Thickness(10) });

					var spFieldRt = new StackPanel { Orientation = Orientation.Vertical };
					{
						spFieldRt.Children.Add(new TextBlock(new Run("Test Profile"))
						{
							FontWeight = FontWeights.Bold,
							Margin = new Thickness(3, 0, 3, 10)
						});
						var fieldResults = (Grid)XamlReader.Parse(XamlWriter.Save(FieldAnalysisTestRight));
						spFieldRt.Children.Add(fieldResults);
					}
					sp.Children.Add(spFieldRt);

					document.Blocks.Add(new BlockUIContainer()
					{
						Child = sp,
					});
				}

				{
					var sp = new StackPanel
					{
						Orientation = Orientation.Vertical,
						HorizontalAlignment = HorizontalAlignment.Left,
					};

					var oxyPlotCanvas = (Canvas)XamlReader.Parse(OxyPlot.Wpf.XamlExporter.ExportToString(ViewModel?.PlotModel, 750, 300));
					sp.Children.Add(oxyPlotCanvas);

					document.Blocks.Add(new BlockUIContainer()
					{
						Child = sp,
						FontSize = 9,
					});
				}

				IDocumentPaginatorSource dps = document;

				try
				{
					pd.PrintDocument(dps.DocumentPaginator, "ProfileComparison");
					MessageBox.Show("The document was printed!", "Success!", MessageBoxButton.OK, MessageBoxImage.Information);
				}
				catch (Exception)
				{
					MessageBox.Show("An error occured while printing.\nMaybe the document is open somewhere?", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void Button_Print_Click(object sender, RoutedEventArgs e)
		{
			PrintVisualToPdf(ResultGrid);
		}

		private void TextBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return ||
				e.Key == Key.Enter)
			{
				var binding = BindingOperations.GetBindingExpression(sender as TextBox, TextBox.TextProperty);
				binding?.UpdateSource();
			}
		}
	}
}