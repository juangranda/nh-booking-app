using System.Globalization;
using FluentValidation;
using booking.core.DTOs;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
	public RegisterRequestValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress();
		RuleFor(x => x.Password)
			.NotEmpty().MinimumLength(8)
			.Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
			.Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
			.Matches("[0-9]").WithMessage("Password must contain a digit.");
	}
}

public class OfficeUpsertDtoValidator : AbstractValidator<OfficeUpsertDto>
{
	public OfficeUpsertDtoValidator()
	{
		RuleFor(x => x.name).NotEmpty().MaximumLength(100);
		RuleFor(x => x.image).NotEmpty();
	}
}

public class ServiceUpsertDtoValidator : AbstractValidator<ServiceUpsertDto>
{
	public ServiceUpsertDtoValidator()
	{
		RuleFor(x => x.name).NotEmpty().MaximumLength(100);
		RuleFor(x => x.officeId).GreaterThan(0);
		RuleFor(x => x.quantity).GreaterThanOrEqualTo(0).When(x => x.quantity.HasValue);
		RuleFor(x => x.maxBookingsPerWeek).GreaterThan(0).When(x => x.maxBookingsPerWeek.HasValue);
		RuleFor(x => x.serviceDetails).NotNull();
		RuleForEach(x => x.serviceDetails).ChildRules(detail =>
		{
			detail.RuleFor(d => d.name).NotEmpty().MaximumLength(100);
			detail.RuleFor(d => d.capacity).GreaterThan(0).When(d => d.capacity.HasValue);
		});
	}
}

public class AvailabilityValidator : AbstractValidator<Availability>
{
	public const int MinFrequency = 15;
	public const int MaxFrequency = 120;

	public AvailabilityValidator()
	{
		RuleFor(x => x.id).GreaterThan(0).WithMessage("'id' must be a service detail id.");
		RuleFor(x => x.date)
			.Must(d => DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
			.WithMessage("'date' must be a date in yyyy-MM-dd format.");

		RuleFor(x => x.frequency)
			.Must(f => int.TryParse(f, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
				&& minutes is >= MinFrequency and <= MaxFrequency)
			.WithMessage($"'frequency' must be a whole number of minutes between {MinFrequency} and {MaxFrequency}.");
	}
}
