using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WOTRMultiplayer.Entities
{
    public class NetworkRandomEncounter
    {
        public Dictionary<string, int> SpecialEncounters { get; set; } = [];

        public float HoursPassedBeforeEncounter { get; set; }

        public int GuardSlotRoll { get; set; }

        public int CamouflageRoll { get; set; }

        public int? RandomUnitSeed { get; set; }

        public Dictionary<string, float> PlaceUnitsInCampRangedRolls { get; set; } = [];

        public Dictionary<string, string> PlaceUnitsInCampRangedTargetRolls { get; set; } = [];

        public Dictionary<string, float> PlaceUnitsInCampUnitYRolls { get; set; } = [];

        public Dictionary<string, float> PlaceUnitsInCampUnitEndPositionRolls { get; set; } = [];

        public float PlaceUnitsOutsideOfCampSharedYRoll { get; set; }

        public Dictionary<string, float> PlaceUnitsOutsideOfCampUnitYRolls { get; set; } = [];

        public Dictionary<string, float> PlaceUnitsOutsideOfCampUnitEndPositionRolls { get; set; } = [];

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"SpecialEncounters={string.Join(", ", SpecialEncounters.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"HoursPassedBeforeEncounter={HoursPassedBeforeEncounter}");
            sb.AppendLine($"GuardSlotRoll={GuardSlotRoll}");
            sb.AppendLine($"CamouflageRoll={CamouflageRoll}");
            sb.AppendLine($"RandomUnitSeed={RandomUnitSeed}");
            sb.AppendLine($"PlaceUnitsInCampRangedRolls={string.Join(", ", PlaceUnitsInCampRangedRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"PlaceUnitsInCampRangedTargetRolls={string.Join(", ", PlaceUnitsInCampRangedTargetRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"PlaceUnitsInCampUnitYRolls={string.Join(", ", PlaceUnitsInCampUnitYRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"PlaceUnitsInCampUnitEndPositionRolls={string.Join(", ", PlaceUnitsInCampUnitEndPositionRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"PlaceUnitsOutsideOfCampSharedYRoll={PlaceUnitsOutsideOfCampSharedYRoll}");
            sb.AppendLine($"PlaceUnitsOutsideOfCampUnitYRolls={string.Join(", ", PlaceUnitsOutsideOfCampUnitYRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");
            sb.AppendLine($"PlaceUnitsOutsideOfCampUnitEndPositionRolls={string.Join(", ", PlaceUnitsOutsideOfCampUnitEndPositionRolls.Select(x => $"{{{x.Key}, {x.Value}}}"))}");

            return sb.ToString();
        }
    }
}
