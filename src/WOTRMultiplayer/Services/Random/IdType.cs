using System.ComponentModel;

namespace WOTRMultiplayer.Services.Random
{
    /// <summary>
    /// Description attribute value is used as prefix during ID generation
    /// </summary>
    public enum IdType
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
        AreaEffect,

        [Description("AD")]
        AbilityData,

        [Description("CCU")]
        CustomCompanionUnit
    }
}
