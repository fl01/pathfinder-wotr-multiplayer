using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLootableEntitySkinned)]
    public class NotifyLootableEntitySkinned : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLootableEntity Entity { get; set; }
    }
}
