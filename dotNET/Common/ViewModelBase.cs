using System.ComponentModel;

namespace VAT.Common
{
	/// <summary>
	/// Implementation of <see cref="INotifyPropertyChanged"/> to simplify models.
	/// </summary>
	public abstract class ViewModelBase : VPropertyChanged
    {
        public ViewModelBase()
        {
        }

        public virtual void Initialize()
        {
        }
	}
}