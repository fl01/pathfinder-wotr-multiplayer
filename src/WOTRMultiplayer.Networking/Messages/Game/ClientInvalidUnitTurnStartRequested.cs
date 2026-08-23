using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.ClientInvalidUnitTurnStartRequested)]
    public class ClientInvalidUnitTurnStartRequested
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
