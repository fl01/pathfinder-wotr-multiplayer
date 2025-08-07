using System.ComponentModel;

namespace WOTRMultiplayer.Random
{
    public enum UniqueIdType
    {
        [Description("UN")]
        Unit,

        [Description("CBU")]
        ChangeBlueprintUnit,

        [Description("IE")]
        ItemEntity,

        [Description("FA")]
        Fact,

        [Description("EV")]
        EntityView
    }
}
