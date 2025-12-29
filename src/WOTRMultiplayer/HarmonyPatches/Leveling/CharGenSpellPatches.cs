using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Spells;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Spells;
using UniRx;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class CharGenSpellPatches
    {
        [HarmonyPatch(typeof(CharGenSpellSelectorItemPCView), nameof(CharGenSpellSelectorItemPCView.OnClick))]
        [HarmonyPrefix]
        public static bool CharGenSpellSelectorItemPCView_OnClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(CharGenSpellsPhaseVM), nameof(CharGenSpellsPhaseVM.OnSpellRemoved))]
        [HarmonyPrefix]
        public static void CharGenSpellsPhaseVM_OnSpellRemoved_Prefix(CollectionRemoveEvent<CharGenSpellSelectorItemVM> spellItem)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var spell = CreateSpell(spellItem.Value);
            Main.Multiplayer.OnLevelingSpellRemoved(spell);
        }

        [HarmonyPatch(typeof(CharGenSpellsPhaseVM), nameof(CharGenSpellsPhaseVM.OnSpellChosen))]
        [HarmonyPrefix]
        public static void CharGenSpellsPhaseVM_OnSpellChosen_Prefix(CollectionAddEvent<CharGenSpellSelectorItemVM> spellItem)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var spell = CreateSpell(spellItem.Value);
            Main.Multiplayer.OnLevelingSpellChosen(spell);
        }

        private static NetworkLevelingSpell CreateSpell(CharGenSpellSelectorItemVM item)
        {
            var spell = new NetworkLevelingSpell
            {
                Name = item.Spell.NameForAcronym,
                Id = item.Spell.AssetGuid.ToString()
            };

            return spell;
        }
    }
}
