using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalCombatTotalDefenseUsed)]
    public class NotifyTacticalCombatTotalDefenseUsed : IForwardableMessage
    {
    }
}
