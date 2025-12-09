using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkPlayer
    {
        [ProtoMember(1)]
        public long Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public bool IsReady { get; set; }

        [ProtoMember(4)]
        public string SaveGameSyncStatus { get; set; }

        [ProtoMember(5)]
        public NetworkContentState ContentState { get; set; }

        [ProtoMember(6)]
        public bool IsHost { get; set; }
    }
}
