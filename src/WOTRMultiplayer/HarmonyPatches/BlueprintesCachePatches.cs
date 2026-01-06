using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.BundlesLoading;
using Kingmaker.UI.MVVM._PCView.Common;
using Microsoft.Extensions.Logging;
using TMPro;

namespace WOTRMultiplayer.HarmonyPatches
{
    [HarmonyPatch]
    public class BlueprintesCachePatches
    {
        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        [HarmonyPostfix]
        public static void BlueprintesCachePatches_Init_Postfix()
        {
            Main.GetLogger<BlueprintesCachePatches>().LogInformation("Applying patch. MethodName={MethodName}", MethodBase.GetCurrentMethod().Name);

            Main.Initialize();

            SavePrefabsFromMainMenu();
            SavePrefabsFromCommonPcView();
        }

        private static void SavePrefabsFromCommonPcView()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("commonpcview.res");
            var commonPcViewGameObject = bundle.LoadAllAssets<UnityEngine.GameObject>().First();
            var saveLoad = commonPcViewGameObject.GetComponent<CommonPCView>().m_SaveLoadPCView;
            Main.Multiplayer.Factory.StoreSaveLoadPCViewPrefab(saveLoad);

            var screen = saveLoad.gameObject.transform.Find("SaveLoadScreen");
            Main.Multiplayer.Factory.StoreBackgroundArt(screen.Find("PapperBackground").gameObject);

            var saveList = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
            Main.Multiplayer.Factory.StoreBorderDecoration(saveList.Find("Decoration").gameObject);

            var title = screen.Find("SaveLoadDetails").Find("Title");
            var defaultTextMesh = title.GetComponentInChildren<TextMeshProUGUI>();
            Main.Multiplayer.Factory.StoreDefaultTextMesh(defaultTextMesh);
        }

        private static void SavePrefabsFromMainMenu()
        {
            var bundle = BundlesLoadService.Instance.RequestBundle("mainmenupcview.res");
            var mainMenuViewGameObject = bundle.LoadAllAssets<UnityEngine.GameObject>().First();
            var creditsSearchPanel = mainMenuViewGameObject.transform.Find("Canvas/Credits_Legacy/CreditsScreen/SearchPanel");

            var inputPrefab = creditsSearchPanel.Find("Input_Field");
            Main.Multiplayer.Factory.StoreInputPrefab(inputPrefab.gameObject);

            var buttonPrefab = creditsSearchPanel.Find("SearchButton");
            Main.Multiplayer.Factory.StoreButtonPrefab(buttonPrefab.gameObject);
        }
    }
}
