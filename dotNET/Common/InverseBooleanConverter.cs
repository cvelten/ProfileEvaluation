using System;
using System.Globalization;
using System.Windows.Data;

namespace VAT.Common
{
    [ValueConversion(typeof(bool), typeof(bool))]
    internal class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool) && targetType != typeof(bool?) && targetType != typeof(object))
                throw new InvalidOperationException("The target must be a boolean");

            if (targetType == typeof(bool?))
            {
                var b = (bool?)value;
                return b.HasValue && !b.Value;
            }
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
