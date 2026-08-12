using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.BundlesLoading;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UnitLogic.Class.Kineticist.Properties;
using Kingmaker.UnitLogic.Mechanics.Properties;
using Microsoft.Extensions.Logging;
using TMPro;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class BlueprintsCachePatches
    {
        public static string LastInitError { get; private set; }

        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        [HarmonyPostfix]
        public static void BlueprintsCache_Init_Postfix()
        {
            Main.GetLogger<BlueprintsCachePatches>().LogInformation("Applying patch. MethodName={MethodName}", MethodBase.GetCurrentMethod().Name);

            Main.Initialize();

            SavePrefabsFromMainMenu();
            SavePrefabsFromCommonPcView();

            ValidateBlueprints();
        }

        /// <summary>
        /// there is no clear reason why, but fighting defensively stops applying at some point (after attack, all regular conditions are met, etc..)
        /// GameLog contains errors related to reading blueprints (21a3e92a7b0f37d4e8581f7992864f30 and 7e0d8bc634c04863aa2b891e750ec8df)
        /// specifically: Field m_Progression defined on type Kingmaker.UnitLogic.Mechanics.Properties.PropertySettings is not a field on the target object which is of type Kingmaker.Designers.Mechanics.Buffs.DamageBonusConditional.
        /// bugged behavior persists until you relaunch the game - probably because blueprints are cached
        /// not sure if this validation is even doing anything, but let's hope it can at least cache somewhat valid blueprint
        /// </summary>
        private static void ValidateBlueprints()
        {
            if (!ValidateFightingDefensivelyBlueprint(out var defError))
            {
                LastInitError = $"FightingDefensivelyAttackPenaltyProperty (21a3e92a7b0f37d4e8581f7992864f30) blueprint is invalid. This might cause issues in multiplayer.{Environment.NewLine}Error:{Environment.NewLine}{defError}";
            }
            else if (!ValidateMountedShieldCombatBlueprint(out var mountError))
            {
                LastInitError = $"MountedShieldUnitProperty (7e0d8bc634c04863aa2b891e750ec8df) blueprint is invalid. This might cause issues in multiplayer.{Environment.NewLine}Error:{Environment.NewLine}{mountError}";
            }
        }

        private static bool ValidateFightingDefensivelyBlueprint(out string error)
        {
            try
            {
                var id = BlueprintGuid.Parse("21a3e92a7b0f37d4e8581f7992864f30");
                var fightingDefensively = ResourcesLibrary.TryGetBlueprint(id);
                if (fightingDefensively is not BlueprintUnitProperty blueprintUnitProperty)
                {
                    error = "Missing blueprint";
                    return false;
                }

                if (fightingDefensively.HasErrors)
                {
                    error = string.Join(";", fightingDefensively.Errors);
                    return false;
                }

                foreach (var component in blueprintUnitProperty.ComponentsArray)
                {
                    if (component is FightingDefensivelyAttackPenaltyProperty fighting)
                    {
                        if (fighting.HalfBuff == null || fighting.HalfBuff.HasErrors)
                        {
                            error = "HalfBuff is corrupted";
                            return false;
                        }

                        foreach (var feature in fighting.Features)
                        {
                            if (feature == null || feature.HasErrors)
                            {
                                error = "Features are corrupted";
                                return false;
                            }
                        }

                        foreach (var feature in fighting.DuelingFeatures)
                        {
                            if (feature == null || feature.HasErrors)
                            {
                                error = "DuelingFeatures are corrupted";
                                return false;
                            }
                        }

                        if (fighting.Settings?.m_Progression == null)
                        {
                            error = "m_Progression field is unavailable";
                            return false;
                        }
                    }
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private static bool ValidateMountedShieldCombatBlueprint(out string error)
        {
            try
            {
                var id = BlueprintGuid.Parse("7e0d8bc634c04863aa2b891e750ec8df");
                var shieldCombat = ResourcesLibrary.TryGetBlueprint(id);
                if (shieldCombat is not BlueprintUnitProperty blueprintUnitProperty)
                {
                    error = "Missing blueprint";
                    return false;
                }

                if (shieldCombat.HasErrors)
                {
                    error = string.Join(";", shieldCombat.Errors);
                    return false;
                }

                foreach (var component in blueprintUnitProperty.ComponentsArray)
                {
                    if (component is ShieldBonusGetter bonusGetter)
                    {
                        if (bonusGetter.Settings?.m_Progression == null)
                        {
                            error = "m_Progression field is unavailable";
                            return false;
                        }
                    }
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private static void SavePrefabsFromCommonPcView()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("commonpcview.res");
            var commonViewGameObject = bundle.LoadAllAssets<UnityEngine.GameObject>().First();
            var commonView = commonViewGameObject.GetComponent<CommonPCView>();
            var saveLoad = commonView.m_SaveLoadPCView;
            Main.Multiplayer.UIFactory.StoreSaveLoadPCViewPrefab(saveLoad);

            var screen = saveLoad.gameObject.transform.Find("SaveLoadScreen");
            Main.Multiplayer.UIFactory.StoreBackgroundArt(screen.Find("PapperBackground").gameObject);

            var saveList = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
            Main.Multiplayer.UIFactory.StoreBorderDecoration(saveList.Find("Decoration").gameObject);

            var title = screen.Find("SaveLoadDetails").Find("Title");
            var defaultTextMesh = title.GetComponentInChildren<TextMeshProUGUI>();
            Main.Multiplayer.UIFactory.StoreDefaultTextMesh(defaultTextMesh);

            var escMenuView = commonView.m_EscMenuContextPCView.m_EscMenuPCView;
            var closeButtonObject = escMenuView.gameObject.transform.Find("Window/Close").gameObject;
            Main.Multiplayer.UIFactory.StoreCloseButtonPrefab(closeButtonObject);
        }

        private static void SavePrefabsFromMainMenu()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("mainmenupcview.res");
            var mainMenuViewGameObject = bundle.LoadAllAssets<UnityEngine.GameObject>().First();
            var creditsSearchPanel = mainMenuViewGameObject.transform.Find("Canvas/Credits_Legacy/CreditsScreen/SearchPanel");

            var inputPrefab = creditsSearchPanel.Find("Input_Field");
            Main.Multiplayer.UIFactory.StoreInputPrefab(inputPrefab.gameObject);

            var buttonPrefab = creditsSearchPanel.Find("SearchButton");
            Main.Multiplayer.UIFactory.StoreButtonPrefab(buttonPrefab.gameObject);
        }
    }
}
