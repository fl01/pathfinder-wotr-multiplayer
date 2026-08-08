using HarmonyLib;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MagicHack;
using WOTRMultiplayer.Entities.SpellbookManagement;

namespace WOTRMultiplayer.HarmonyPatches.SpellbookManagement
{
    [HarmonyPatch]
    public class MagicHackPatches
    {
        [HarmonyPatch(typeof(SpellbookMagicHackMixerVM), nameof(SpellbookMagicHackMixerVM.TryWriteNewSpell))]
        [HarmonyPrefix]
        public static void SpellbookMagicHackMixerVM_TryWriteNewSpell_Prefix(SpellbookMagicHackMixerVM __instance)
        {
            if (!Main.Multiplayer.IsActive || !__instance.CanCombine.Value)
            {
                return;
            }

            var spell = new NetworkMagicHackSpell
            {
                Index = __instance.SelectedSlotIndex,
                SpellbookId = __instance.Spellbook.Value.Blueprint.AssetGuid.ToString(),
                UnitId = __instance.Unit.Value.Unit.UniqueId,
                SpellLevel = __instance.m_CurrentSpell.SpellLevel,
                AbilityBlueprintId = __instance.m_CurrentSpell.Blueprint.AssetGuid.ToString(),
                DefaultBlueprintId = __instance.m_SelectedDefaultBlueprint?.AssetGuid.ToString(),
                TouchBlueprintId = __instance.m_SelectedTouchBlueprint?.AssetGuid.ToString(),
                MagicHackData = Main.Mapper.Map<NetworkMagicHackData>(__instance.m_CurrentSpell.MagicHackData)
            };
            Main.Multiplayer.OnSpellbookMagicHackSpellCreated(spell);
        }
    }
}
