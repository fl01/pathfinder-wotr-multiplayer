using FluentValidation;

namespace WOTRMultiplayer.Services.Settings.Validators
{
    public class NetworkPortValidator : AbstractValidator<int>
    {
        public const int MinPort = 1024;

        public const int MaxPort = ushort.MaxValue;

        public const int MaxCharacters = 5;

        public NetworkPortValidator()
        {
            RuleFor(x => x).GreaterThanOrEqualTo(MinPort);
            RuleFor(x => x).LessThanOrEqualTo(MaxPort);
        }
    }
}
