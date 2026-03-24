using System;

namespace WOTRMultiplayer.Services.Random
{
    [Flags]
    public enum SeedKind
    {
        Session = 1 << 0,
        LoadedSaveSeed = 1 << 1,
        AreaSeed = 1 << 2,
        CombatSeed = 1 << 3,
        CombatTurnSeed = 1 << 4,
        CrusadeArmyCombatAreaSeed = 1 << 5,
        CrusadeArmyCombatSeed = 1 << 6,

        All = Session | LoadedSaveSeed | AreaSeed | CombatSeed | CombatTurnSeed | CrusadeArmyCombatAreaSeed | CrusadeArmyCombatSeed
    }
}
