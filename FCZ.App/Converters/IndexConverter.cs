using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;
using System.Collections;

namespace FCZ.App.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IList list && parameter is ListViewItem item)
            {
                var index = list.IndexOf(item.DataContext);
                return index >= 0 ? (index + 1).ToString() : string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


