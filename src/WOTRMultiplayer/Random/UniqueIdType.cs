using System.ComponentModel;

namespace WOTRMultiplayer.Random
{
    /// <summary>
    /// Description attribute value is used as prefix during ID generation
    /// </summary>
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
        EntityView,

        [Description("AE")]
        AreaEffect
    }
}
