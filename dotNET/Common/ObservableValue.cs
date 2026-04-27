using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VAT.Common
{
	public class ObservableValue<T> : INotifyPropertyChanged
	{
		private T _value;

		public T Value
		{
			get => _value;
			set
			{
				if (value != null && value.Equals(_value))
					return;
				_value = value;
				OnPropertyChanged(nameof(Value));
			}
		}

		public Action OnChanged { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public ObservableValue() {}
		public ObservableValue(Action onChanged)
		{
			OnChanged = onChanged;
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			OnChanged?.Invoke();
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public string ToString(bool underlyingValue) => underlyingValue ? _value.ToString() : ToString();
	}
}
