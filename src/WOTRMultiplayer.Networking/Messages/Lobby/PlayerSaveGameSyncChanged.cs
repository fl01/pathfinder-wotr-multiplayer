using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.PlayerSaveGameSyncChanged)]
    public class PlayerSaveGameSyncChanged
    {
        [ProtoMember(1)]
        public bool IsSynced { get; set; }
    }
}
