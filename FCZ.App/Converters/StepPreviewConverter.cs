using System;
using System.Globalization;
using System.Windows.Data;
using FCZ.Core.Models;

namespace FCZ.App.Converters
{
    public class StepPreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Step step)
            {
                return step switch
                {
                    WaitForImageThenClickStep s => $"Template: {s.Template}, Region: ({s.Region.X},{s.Region.Y}) {s.Region.Width}x{s.Region.Height}",
                    WaitForImageStep s => $"Template: {s.Template}, Region: ({s.Region.X},{s.Region.Y})",
                    ClickTemplateStep s => $"Template: {s.Template}, Region: ({s.Region.X},{s.Region.Y})",
                    ClickPointStep s => $"Point: ({s.Point.X}, {s.Point.Y})",
                    TypeTextStep s => $"Text: {s.Text}...",
                    WaitStep s => $"{s.Ms}ms",
                    LogStep s => $"Log: {s.Message}",
                    ConditionalBlockStep s => $"Condition: {s.Condition.Kind}",
                    LoopStep s => $"Repeat: {s.Repeat} times",
                    _ => step.Type
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


