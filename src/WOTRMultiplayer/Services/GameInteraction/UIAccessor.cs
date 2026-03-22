using Kingmaker;
using Kingmaker.Settings;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._PCView.CityBuilder;
using Kingmaker.UI.MVVM._PCView.CombatLog;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.Common.MessageModal;
using Kingmaker.UI.MVVM._PCView.Crusade.Armies;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._PCView.Crusade.CombatResult;
using Kingmaker.UI.MVVM._PCView.Crusade.LeaderLevelUp;
using Kingmaker.UI.MVVM._PCView.Crusade.PointerMarker;
using Kingmaker.UI.MVVM._PCView.Crusade.Recruit;
using Kingmaker.UI.MVVM._PCView.Dialog;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Menu;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Message;
using Kingmaker.UI.MVVM._PCView.GlobalMap.Toolbar;
using Kingmaker.UI.MVVM._PCView.GroupChanger;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._PCView.Kingdom;
using Kingmaker.UI.MVVM._PCView.Kingdom.KingdomInfo;
using Kingmaker.UI.MVVM._PCView.Loot;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._PCView.MapIslands;
using Kingmaker.UI.MVVM._PCView.NewGame;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.Metamagic;
using Kingmaker.UI.MVVM._PCView.TacticalCombat;
using Kingmaker.UI.MVVM._PCView.TacticalCombat.Result;
using Kingmaker.UI.MVVM._PCView.Transition;
using Kingmaker.UI.MVVM._VM.ServiceWindows;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Inventory;
using Kingmaker.UI.MVVM._VM.Vendor;
using WOTRMultiplayer.Abstractions.GameInteraction;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class UIAccessor : IUIAccessor
    {
        private TacticalCombatPCView TacticalCombatPCView => Game.Instance.RootUiContext.m_UIView as TacticalCombatPCView;
        private MainMenuPCView MainMenuPCView => Game.Instance.RootUiContext.m_UIView as MainMenuPCView;

        public InGamePCView InGamePCView => Game.Instance.RootUiContext.m_UIView as InGamePCView;

        public ServiceWindowsVM ServiceWindowsVM => (Game.Instance.RootUiContext.InGameVM?.StaticPartVM?.ServiceWindowsVM ?? Game.Instance.RootUiContext?.GlobalMapVM?.ServiceWindowsVM);

        public CommonPCView CommonPCView => (Game.Instance.RootUiContext.m_CommonView as CommonPCView);

        public EscMenuPCView EscMenu => CommonPCView?.m_EscMenuContextPCView?.m_EscMenuPCView;

        public GlobalMapPCView GlobalMapPCView => Game.Instance.RootUiContext.m_UIView as GlobalMapPCView;

        public KingdomPCView KingdomPCView => Game.Instance.RootUiContext.m_UIView as KingdomPCView;

        public CityBuilderPCView CityBuilderPCView => Game.Instance.RootUiContext.m_UIView as CityBuilderPCView;

        public NewGamePCView NewGamePCView => MainMenuPCView?.NewGamePCView;

        public LootPCView LootPCView => InGamePCView?.m_StaticPartPCView?.m_LootContextPCView?.m_LootPCView;

        public LootCollectorPCView LootCollector => LootPCView?.m_Collector;

        public PartyPCView PartyPCView => InGamePCView?.m_StaticPartPCView?.m_PartyPCView ?? GlobalMapPCView?.m_PartyPCView;

        public SkipTimePCView SkipTimeView => InGamePCView?.m_StaticPartPCView?.m_SkipTimePCView ?? GlobalMapPCView?.m_SkipTimePCView;

        public RestPCView RestView => InGamePCView?.m_StaticPartPCView?.m_RestContextPCView?.m_RestPCView ?? GlobalMapPCView?.m_RestPCView;

        public GroupChangerPCView GroupChangerView => (InGamePCView?.m_StaticPartPCView?.m_GroupChangerContextPCView ?? GlobalMapPCView?.m_GroupChangerContextPCView)?.m_GroupChangerPCView;

        public VendorVM VendorVM => InGamePCView?.m_StaticPartPCView?.m_VendorPCView?.ViewModel;

        public SpellbookPCView SpellbookPCView => (InGamePCView?.m_StaticPartPCView?.m_ServiceWindowsPCView ?? GlobalMapPCView.m_ServiceWindowsPCView)?.m_SpellbookPCView;

        public SpellbookMemorizingPanelPCView SpellbookMemorizingPanelPCView => SpellbookPCView?.m_MemorizingPanelView;

        public SpellbookMetamagicMixerPCView SpellbookMetamagicMixerPCView => SpellbookPCView?.m_MetamagicMixerView;

        public CharGenPCView CharGenView => (InGamePCView?.m_StaticPartPCView?.m_CharGenContextPCView ?? GlobalMapPCView?.m_CharGenContextPCView ?? MainMenuPCView?.m_CharGenContextPCView)?.m_CharGenPCView;

        public RespecWindowPCView RespecView => (InGamePCView?.m_StaticPartPCView?.m_CharGenContextPCView ?? GlobalMapPCView?.m_CharGenContextPCView)?.m_RespecWindowPCView;

        public InventoryVM InventoryVM => ServiceWindowsVM?.InventoryVM?.Value;

        public CombatLogPCView CombatLogPCView => InGamePCView?.m_StaticPartPCView?.m_CombatLogPCView ?? GlobalMapPCView?.m_CombatLogPCView ?? TacticalCombatPCView?.m_CombatLogPCView;

        public DialogContextPCView DialogContextPCView => InGamePCView?.m_StaticPartPCView?.m_DialogContextPCView ?? GlobalMapPCView?.m_DialogContextPCView;

        public TacticalCombatResultsPCView TacticalCombatResultsPCView => GlobalMapPCView?.m_AutoCombatResultsPCView ?? TacticalCombatPCView?.m_TacticalCombatResultsPCView ?? KingdomPCView?.m_AutoCombatResultsPCView;

        public GlobalMapToolbarPCView GlobalMapToolbarPCView => GlobalMapPCView?.m_GlobalMapToolbarPCView ?? KingdomPCView?.m_GlobalMapToolbarPCView;
        public GlobalMapCrusadeArmiesPCView GlobalMapCrusadeArmiesPCView => GlobalMapPCView?.m_ArmiesPCView ?? KingdomPCView?.m_ArmiesPCView;
        public ArmyCartBuyLeaderPCView ArmyCartBuyLeaderPCView => GlobalMapPCView?.m_BuyLeaderPCView ?? KingdomPCView?.m_BuyLeaderPCView;
        public ArmyInfoHUDPCView ArmyInfoHUDPCView => GlobalMapPCView?.m_ArmyInfoHUDPCView ?? KingdomPCView?.m_ArmyInfoHUDPCView;
        public ArmyInfoPCView ArmyInfoPCView => GlobalMapPCView?.m_ArmyInfoPCView ?? KingdomPCView?.m_ArmyInfoPCView;
        public KingdomInfoPCView KingdomInfoPCView => GlobalMapPCView?.m_KingdomInfoPCView ?? KingdomPCView?.m_KingdomInfoPCView;
        public GlobalMapMenuPCView GlobalMapMenuPCView => GlobalMapPCView?.m_GlobalMapMenuPCView ?? KingdomPCView?.m_GlobalMapMenuPCView;
        public GlobalMapArmyPointerMarkerPCView GlobalMapArmyPointerMarkerPCView => GlobalMapPCView?.m_GlobalMapArmyPointerMarkerPCView ?? KingdomPCView?.m_GlobalMapArmyPointerMarkerPCView;
        public GlobalMapEnterMessagePCView GlobalMapEnterMessagePCView => GlobalMapPCView?.m_GlobalMapEnterMessagePCView ?? KingdomPCView?.m_GlobalMapEnterMessagePCView;
        public RecruitPCView RecruitPCView => GlobalMapPCView?.m_RecruitPCView ?? KingdomPCView?.m_RecruitPCView;
        public CombatResultPCView CombatResultPCView => GlobalMapPCView?.m_CombatResultPCView ?? KingdomPCView?.m_CombatResultPCView;
        public LeaderLevelUpPCView LeaderLevelUpPCView => GlobalMapPCView?.m_LeaderLevelUpPCView ?? KingdomPCView?.m_LeaderLevelUpPCView;

        public TransitionPCView TransitionPCView => InGamePCView?.m_StaticPartPCView?.m_TransitionPCView ?? GlobalMapPCView?.m_TransitionPCView;
        public MapIslandsPCView MapIslandsPCView => InGamePCView?.m_StaticPartPCView?.m_CreatedMapIslandsPCView;

        public MainMenuSideBarPCView MainMenuSideBarPCView => MainMenuPCView?.m_MainMenuSideBarPCView;

        public void CloseAllWindows()
        {
            ServiceWindowsVM?.HandleCloseAll();

            var saveLoadVM = CommonPCView?.m_SaveLoadPCView?.ViewModel;
            saveLoadVM?.OnClose();

            var settingsVM = CommonPCView?.m_SettingsPCView?.ViewModel;
            if (settingsVM != null)
            {
                if (SettingsController.HasUnconfirmedSettings())
                {
                    settingsVM.RevertSettings();
                }

                settingsVM.m_CloseAction?.Invoke();
            }

            Main.Lobby.CloseWindow();
        }
    }
}
