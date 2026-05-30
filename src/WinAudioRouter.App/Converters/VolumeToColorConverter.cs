using System.Globalization;

namespace WinAudioRouter.App.Converters;

public class VolumeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int volumeLevel)
        {
            if (volumeLevel <= 30)
                return Colors.Red;
            if (volumeLevel <= 70)
                return Colors.Orange;
            return Colors.Green;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
