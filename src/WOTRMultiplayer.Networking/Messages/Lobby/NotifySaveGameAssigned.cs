using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifySaveGameAssigned)]
    public class NotifySaveGameAssigned
    {
        [ProtoMember(1)]
        public byte[] Content { get; set; }

        /// <summary>
        /// means game should be loaded instantly aka quick load
        /// </summary>
        [ProtoMember(2)]
        public bool IsForceLoad { get; set; }

        [ProtoMember(3)]
        public string GameId { get; set; }
    }
}
