namespace booking.core.Options;

public class SeedOptions
{
	public const string SectionName = "Seed";

	public bool? DemoData { get; set; }

	// Optional first admin for non-demo environments. Set both (e.g. Seed__AdminEmail and Seed__AdminPassword environment variables); the account is only created if that email
	// doesn't exist yet, so the values can be removed after the first start.
	public string? AdminEmail { get; set; }
	public string? AdminPassword { get; set; }
}
