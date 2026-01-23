namespace WOTRMultiplayer.Entities.GlobalMap
{
    public class NetworkGlobalMapUnitRecruitmentOrder
    {
        public string ArmyId { get; set; }

        public string BlueprintId { get; set; }

        public int Count { get; set; }

        public NetworkGlobalMapUnitRecruitmentType Type { get; set; }
    }
}
