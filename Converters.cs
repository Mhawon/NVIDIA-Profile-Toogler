using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NVIDIA_Profil_Toogler
{
    /// <summary>
    /// Converts between float values (0.0 - 1.0) and slider double values (0 - 100).
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is float f)
            {
                return (double)f * 100.0;
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return (float)(d / 100.0);
            }
            return 0.0f;
        }
    }
}
