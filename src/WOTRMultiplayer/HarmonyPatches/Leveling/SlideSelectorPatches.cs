using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.MVVM._CommonView.CharGen.Phases.Common;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Appearance;
using Kingmaker.UI.MVVM._PCView.CharGen.Phases.Common;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Common;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.SelectionGroup;
using UniRx;
using WOTRMultiplayer.Entities.Leveling;

namespace WOTRMultiplayer.HarmonyPatches.Leveling
{
    [HarmonyPatch]
    public class SlideSelectorPatches
    {
        [HarmonyPatch(typeof(SelectionGroupEntityVM), nameof(SelectionGroupEntityVM.SetSelectedFromView))]
        [HarmonyPrefix]
        public static bool SlideSelectorCommonView_SetSelectedFromView_Prefix(SelectionGroupEntityVM __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance is not TextureSelectorItemVM || GetCurrentCharGenDetailView() is not CharGenAppearancePhaseDetailedPCView)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            return canContinue;
        }

        [HarmonyPatch(typeof(SlideSelectorCommonView), nameof(SlideSelectorCommonView.OnPreviousHandler))]
        [HarmonyPrefix]
        public static bool SlideSelectorCommonView_OnPreviousHandler_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || GetCurrentCharGenDetailView() is not CharGenAppearancePhaseDetailedPCView)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            if (!canContinue)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(SlideSelectorCommonView), nameof(SlideSelectorCommonView.OnNextHandler))]
        [HarmonyPrefix]
        public static bool SlideSelectorCommonView_OnNextHandler_Prefix(ref bool __result)
        {
            if (!Main.Multiplayer.IsActive || GetCurrentCharGenDetailView() is not CharGenAppearancePhaseDetailedPCView)
            {
                return true;
            }

            var canContinue = Main.Multiplayer.CanMakeLevelingDecisions();
            if (!canContinue)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(CharGenAppearancePhaseDetailedPCView), nameof(CharGenAppearancePhaseDetailedPCView.BindViewImplementation))]
        [HarmonyPostfix]
        public static void CharGenAppearancePhaseDetailedPCView_BindViewImplementation_Postfix(CharGenAppearancePhaseDetailedPCView __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            __instance.AddDisposable(OnSliderIndexChanged(__instance.m_BodySelectorPcView, OnBodyTypeChanged));
            __instance.AddDisposable(OnTextureSelected(__instance.m_BodyColorSelectorView, OnBodyColorChanged));

            __instance.AddDisposable(OnSliderIndexChanged(__instance.m_FaceSelectorPcView, OnFaceChanged));
            __instance.AddDisposable(OnSliderIndexChanged(__instance.m_ScarSelectorPcView, OnScarChanged));
            __instance.AddDisposable(OnTextureSelected(__instance.m_EyesColorSelectorView, OnEyesColorChanged));

            __instance.AddDisposable(OnSliderIndexChanged(__instance.m_HairSelectorPcView, OnHairStyleChanged));
            __instance.AddDisposable(OnTextureSelected(__instance.m_HairColorSelectorView, OnHairColorChanged));

            __instance.AddDisposable(OnSliderIndexChanged(__instance.m_HornSelectorPcView, OnHornsChanged));
            __instance.AddDisposable(OnTextureSelected(__instance.m_HornColorSelectorView, OnHornColorChanged));

            foreach (var selector in __instance.ViewModel.WarpaintsSelectorVMList)
            {
                __instance.AddDisposable(OnPagedSliderIndexChanged(selector, __instance.ViewModel.WarpaintsSelectorVMList.IndexOf(selector), OnWarpaintChanged));
            }

            foreach (var selector in __instance.ViewModel.WarpaintsColorSelectorVMList)
            {
                __instance.AddDisposable(OnPagedTextureSelected(selector, __instance.ViewModel.WarpaintsColorSelectorVMList.IndexOf(selector), OnWarpaintColorChanged));
            }

            foreach (var selector in __instance.ViewModel.TattoosSelectorVMList)
            {
                __instance.AddDisposable(OnPagedSliderIndexChanged(selector, __instance.ViewModel.TattoosSelectorVMList.IndexOf(selector), OnTattooChanged));
            }

            foreach (var selector in __instance.ViewModel.TattoosColorSelectorVMList)
            {
                __instance.AddDisposable(OnPagedTextureSelected(selector, __instance.ViewModel.TattoosColorSelectorVMList.IndexOf(selector), OnTattooColorChanged));
            }

            __instance.AddDisposable(OnTextureSelected(__instance.m_PrimaryOutfitColorSelectorView, OnPrimaryOutfitColorChanged));
            __instance.AddDisposable(OnTextureSelected(__instance.m_SecondaryOutfitColorSelectorView, OnSecondaryOutfitColorChanged));
        }

        private static IDisposable OnSliderIndexChanged(SlideSelectorPCView slideSelectorPCView, Action<int> handler)
        {
            return slideSelectorPCView.ViewModel.CurrentIndex.Subscribe<int>(handler);
        }

        private static IDisposable OnPagedSliderIndexChanged(StringSequentialSelectorVM stringSequentialSelectorVM, int pageNumber, Action<int, int> handler)
        {
            return stringSequentialSelectorVM.CurrentIndex.Subscribe<int>(i => handler(i, pageNumber));
        }

        private static IDisposable OnTextureSelected(TextureSelectorPCView textureSelectorPCView, Action<TextureSelectorItemVM> handler)
        {
            return textureSelectorPCView.ViewModel.SelectedEntity.Subscribe<TextureSelectorItemVM>(itemVM =>
            {
                if (itemVM == null)
                {
                    return;
                }

                handler(itemVM);
            });
        }

        private static IDisposable OnPagedTextureSelected(SelectionGroupRadioVM<TextureSelectorItemVM> textureSelectorPCView, int pageNumber, Action<TextureSelectorItemVM, int> handler)
        {
            return textureSelectorPCView.SelectedEntity.Subscribe<TextureSelectorItemVM>(itemVM =>
            {
                if (itemVM == null)
                {
                    return;
                }

                handler(itemVM, pageNumber);
            });
        }

        private static void OnBodyTypeChanged(int index)
        {
            Main.Multiplayer.OnLevelingBodyTypeAppearanceChanged(index);
        }

        private static void OnFaceChanged(int index)
        {
            Main.Multiplayer.OnLevelingFaceAppearanceChanged(index);
        }

        private static void OnScarChanged(int index)
        {
            Main.Multiplayer.OnLevelingScarAppearanceChanged(index);
        }

        private static void OnHairStyleChanged(int index)
        {
            Main.Multiplayer.OnLevelingHairStyleAppearanceChanged(index);
        }

        private static void OnHornsChanged(int index)
        {
            Main.Multiplayer.OnLevelingHornsAppearanceChanged(index);
        }

        private static void OnWarpaintChanged(int index, int pageNumber)
        {
            var warpaint = new NetworkLevelingWarpaint
            {
                Index = index,
                PageNumber = pageNumber
            };
            Main.Multiplayer.OnLevelingWarpaintAppearanceChanged(warpaint);
        }

        private static void OnTattooChanged(int index, int pageNumber)
        {
            var tattoo = new NetworkLevelingTattoo
            {
                Index = index,
                PageNumber = pageNumber
            };
            Main.Multiplayer.OnLevelingTattooAppearanceChanged(tattoo);
        }

        private static void OnBodyColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingBodyColorAppearanceChanged(textureName);
        }

        private static void OnEyesColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingEyesColorAppearanceChanged(textureName);
        }

        private static void OnHairColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingHairColorAppearanceChanged(textureName);
        }

        private static void OnHornColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingHornsColorAppearanceChanged(textureName);
        }

        private static void OnWarpaintColorChanged(TextureSelectorItemVM selectorItemVM, int pageNumber)
        {
            var warpaint = new NetworkLevelingWarpaint
            {
                TextureName = selectorItemVM.Texture.Value.name,
                PageNumber = pageNumber
            };
            Main.Multiplayer.OnLevelingWarpaintColorAppearanceChanged(warpaint);
        }

        private static void OnTattooColorChanged(TextureSelectorItemVM selectorItemVM, int pageNumber)
        {
            var tattoo = new NetworkLevelingTattoo
            {
                TextureName = selectorItemVM.Texture.Value.name,
                PageNumber = pageNumber
            };
            Main.Multiplayer.OnLevelingTattooColorAppearanceChanged(tattoo);
        }

        private static void OnPrimaryOutfitColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingPrimaryOutfitColorAppearanceChanged(textureName);
        }

        private static void OnSecondaryOutfitColorChanged(TextureSelectorItemVM selectorItemVM)
        {
            var textureName = selectorItemVM.Texture.Value.name;
            Main.Multiplayer.OnLevelingSecondaryOutfitColorAppearanceChanged(textureName);
        }

        private static ICharGenPhaseDetailedView GetCurrentCharGenDetailView()
        {
            var charGenContext = Game.Instance.RootUiContext.m_UIView switch
            {
                InGamePCView inGamePCView => inGamePCView.m_StaticPartPCView.m_CharGenContextPCView,
                GlobalMapPCView globalMapPCView => globalMapPCView.m_CharGenContextPCView,
                _ => null
            };

            var charGenView = charGenContext?.m_CharGenPCView;
            if (charGenView == null)
            {
                Main.GetLogger<SequentialSelectorCommonViewPatches>().LogError("Unable to find char gen pc view");
                return null;
            }

            return charGenView?.SelectedDetailView;
        }
    }
}
