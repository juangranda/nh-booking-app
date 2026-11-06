namespace booking.core.Constants;

public static class Codes
{
	public const string Ok = "OK";
	public const string Unauthorized = "ERR_UNAUTHORIZED";
	public const string Forbidden = "ERR_FORBIDDEN";

	public const string InvalidCredentials = "ERR_AUTH_INVALID_CREDENTIALS";
	public const string EmailNotConfirmed = "ERR_AUTH_EMAIL_NOT_CONFIRMED";
	public const string UserNotFound = "ERR_AUTH_USER_NOT_FOUND";
	public const string NotFound = "ERR_NOT_FOUND";
	public const string EmailRequired = "ERR_AUTH_EMAIL_REQUIRED";
	public const string PasswordWeak = "ERR_AUTH_PASSWORD_WEAK";
	public const string EmailAlreadyExists = "ERR_AUTH_EMAIL_EXISTS";

	public const string BadRequest = "ERR_BAD_REQUEST";
	public const string Conflict = "ERR_CONFLICT";
	public const string ServerError = "ERR_SERVER";
	public const string InUse = "ERR_IN_USE";

	public const string BookingInvalid = "ERR_BOOKING_INVALID";
	public const string BookingConflict = "ERR_BOOKING_CONFLICT";
	public const string BookingLimitReached = "ERR_BOOKING_LIMIT";
}