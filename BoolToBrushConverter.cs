using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Converts a boolean value to a Brush. Used for highlighting filtered items.
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Brush used when the value is true.
        /// </summary>
        public Brush? MatchedBrush { get; set; }

        /// <summary>
        /// Brush used when the value is false.
        /// </summary>
        public Brush? UnmatchedBrush { get; set; }

        /// <summary>
        /// Converts boolean to Brush.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMatched && isMatched)
            {
                return MatchedBrush ?? (SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.Black);
            }
            
            return UnmatchedBrush ?? new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate-400
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
