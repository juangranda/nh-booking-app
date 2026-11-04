namespace booking.core.Options;

// Business hours for bookings, in office-local time. Bound from the "Booking" config section.
public class BookingOptions
{
	public const string SectionName = "Booking";

	public int OpenHour { get; set; } = 9;
	public int CloseHour { get; set; } = 17;
}
