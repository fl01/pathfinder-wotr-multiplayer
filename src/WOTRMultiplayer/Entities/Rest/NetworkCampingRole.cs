using Kingmaker.Controllers.Rest.State;

namespace WOTRMultiplayer.Entities.Rest
{
    public class NetworkCampingRole
    {
        public CampingRoleType RoleType { get; set; }

        public string PrimaryUnitId { get; set; }

        public string SecondaryUnitId { get; set; }
    }
}
