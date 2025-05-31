using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(109)]
    public class PlayerSaveGameSyncChanged
    {
        [ProtoMember(1)]
        public bool IsSynced { get; set; }
    }
}
