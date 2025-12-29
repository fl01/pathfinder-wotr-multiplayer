using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Services.GameInteraction.Contexts
{
    public class NetworkRandomEncounterContext
    {
        public NetworkRandomEncounter Recording { get; set; }

        public NetworkRandomEncounter PreRecorded { get; set; }
    }
}
