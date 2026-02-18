using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDialogPopup
    {
        [ProtoMember(1)]
        [LogMe]
        public string AreaName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string DialogName { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string CueName { get; set; }
    }
}
