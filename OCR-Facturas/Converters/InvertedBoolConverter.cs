using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OCR_Facturas.Converters
{
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Rara vez se usa el camino de vuelta (de la vista al ViewModel) para este caso
            throw new NotImplementedException();
        }
    }
}
