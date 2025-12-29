using System;
using FluentValidation;

namespace WOTRMultiplayer.Services.Settings.Validators
{
    public class TimeSpanValidator : AbstractValidator<string>
    {
        public const int MaxLength = 24;

        public TimeSpanValidator()
        {
            RuleFor(x => x).MaximumLength(MaxLength);
            RuleFor(x => x).Must(x => TimeSpan.TryParse(x, out _));
        }
    }
}
