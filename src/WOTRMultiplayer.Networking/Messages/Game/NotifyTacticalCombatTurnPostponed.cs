using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalCombatTurnPostponed)]
    public class NotifyTacticalCombatTurnPostponed : IForwardableMessage
    {
    }
}
