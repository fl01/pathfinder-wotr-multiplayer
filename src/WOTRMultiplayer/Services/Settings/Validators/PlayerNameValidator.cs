using FluentValidation;

namespace WOTRMultiplayer.Services.Settings.Validators
{
    public class PlayerNameValidator : AbstractValidator<string>
    {
        public const int MaxLength = 24;

        public PlayerNameValidator()
        {
            RuleFor(x => x).NotEmpty();
            RuleFor(x => x).MaximumLength(MaxLength);
        }
    }
}
