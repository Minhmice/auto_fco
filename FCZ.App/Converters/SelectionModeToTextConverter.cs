using System;
using System.Globalization;
using System.Windows.Data;
using FCZ.App.ViewModels;

namespace FCZ.App.Converters
{
    public class SelectionModeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SelectionMode mode)
            {
                return mode switch
                {
                    SelectionMode.Region => "Region Mode: Drag to select",
                    SelectionMode.Point => "Point Mode: Click to select",
                    _ => "Hotkey: Ctrl+Drag = Region, Ctrl+Click = Point, Esc = Cancel"
                };
            }
            return "No selection mode";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


