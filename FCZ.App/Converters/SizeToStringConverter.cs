using System;
using System.Globalization;
using System.Windows.Data;
using System.Drawing;

namespace FCZ.App.Converters
{
    public class SizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Size size)
            {
                return $"{size.Width}x{size.Height}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


