using System.Windows;

namespace VAT.Common
{
	internal abstract class WindowBase<T> : Window
		where T : ViewModelBase, new()
	{
		public virtual T ViewModel
		{
			get => (T)DataContext;
			set => DataContext = value;
		}

		public WindowBase() : this(new T())
		{
		}

		public WindowBase(T viewModel)
		{
			ViewModel = viewModel;
			Initialized += delegate { ViewModel?.Initialize(); };
		}
	}
}
