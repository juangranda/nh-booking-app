using System.Globalization;

namespace booking.core.Rules;

public static class DurationRange
{
	public static bool TryParse(string? value, out int minMinutes, out int maxMinutes)
	{
		minMinutes = maxMinutes = 0;
		var parts = value?.Split('-', StringSplitOptions.TrimEntries);
		return parts is { Length: 2 }
			&& int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out minMinutes)
			&& int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out maxMinutes)
			&& minMinutes > 0
			&& minMinutes <= maxMinutes;
	}
}
