using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkPlayer
    {
        [ProtoMember(1)]
        [LogMe]
        public long Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool IsReady { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string LobbySyncStatus { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public NetworkContentState ContentState { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool IsHost { get; set; }

        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
