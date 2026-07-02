using Kingmaker.EntitySystem.Entities;
using Kingmaker.TurnBasedMode;
using TurnBased.Controllers;
using static TurnBased.Controllers.TurnController;

namespace WOTRMultiplayer.Extensions
{
    public static class TurnControllerExtensions
    {
        public static MovementLimit? GetMovementLimit(this TurnController turnController, UnitEntityData unit)
        {
            if (turnController == null || unit == null)
            {
                return null;
            }

            // holding shift only affects movement predictions for some reason, instead of setting CurrentMovementLimit
            if (unit.IsCurrentUnit() && PathVisualizer.Instance != null && PathVisualizer.Instance.m_WillUseFiveFootStep > 0)
            {
                return MovementLimit.FiveFootStep;
            }

            return turnController.CurrentMovementLimit;
        }
    }
}
