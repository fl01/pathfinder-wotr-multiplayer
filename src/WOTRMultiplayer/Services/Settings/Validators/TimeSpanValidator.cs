using System;
using System.Globalization;
using FluentValidation;

namespace WOTRMultiplayer.Services.Settings.Validators
{
    public class TimeSpanValidator : AbstractValidator<string>
    {
        public const int MaxLength = 24;

        private static readonly string[] _formats =
        [
            @"hh\:mm\:ss",
            @"hh\:mm\:ss\.f",
            @"hh\:mm\:ss\.ff",
            @"hh\:mm\:ss\.fff",
            @"hh\:mm\:ss\.ffff"
        ];

        public TimeSpanValidator()
        {
            RuleFor(x => x).MaximumLength(MaxLength);
            RuleFor(x => x).Must(x => TimeSpan.TryParseExact(x, _formats, CultureInfo.InvariantCulture, out _));
        }
    }
}
