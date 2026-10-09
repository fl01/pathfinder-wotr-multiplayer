using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Abstractions.GameInteraction.CombatLog
{
    public class PlayerLogParameter : ColorizedParameter
    {
        public PlayerLogParameter(NetworkPlayer player)
            : base(player.Name, player.Color.ToUnityColor())
        {
        }
    }
}
