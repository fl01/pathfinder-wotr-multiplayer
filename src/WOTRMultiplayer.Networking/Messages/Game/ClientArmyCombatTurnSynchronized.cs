using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.ClientArmyCombatTurnSynchronized)]
    public class ClientArmyCombatTurnSynchronized
    {
    }
}
