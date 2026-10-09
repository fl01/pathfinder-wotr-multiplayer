using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalCombatRetreated)]
    public class NotifyTacticalCombatRetreated : IForwardableMessage
    {
    }
}
