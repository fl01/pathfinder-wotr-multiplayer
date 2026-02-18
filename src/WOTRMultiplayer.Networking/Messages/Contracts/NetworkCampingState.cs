using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCampingState
    {
        [ProtoMember(1)]
        [LogMe]
        public string CookingBlueprintRecipeId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string PotionBlueprintRecipeId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string ScrollBlueprintRecipeId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool AutotuneIterationsStatus { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int IterationsCount { get; set; }
    }
}
