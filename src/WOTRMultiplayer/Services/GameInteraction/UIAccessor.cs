using Kingmaker;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.Common.MessageModal;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._PCView.GlobalMap;
using Kingmaker.UI.MVVM._PCView.GroupChanger;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UI.MVVM._PCView.Loot;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._PCView.NewGame;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Inventory;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UI.MVVM._VM.Vendor;
using WOTRMultiplayer.Abstractions.GameInteraction;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class UIAccessor : IUIAccessor
    {
        private InGamePCView InGamePCView => Game.Instance.RootUiContext.m_UIView as InGamePCView;
        private MainMenuPCView MainMenuPCView => Game.Instance.RootUiContext.m_UIView as MainMenuPCView;

        public EscMenuPCView EscMenu => (Game.Instance.RootUiContext.m_CommonView as CommonPCView)?.m_EscMenuContextPCView?.m_EscMenuPCView;

        public GlobalMapPCView GlobalMapPCView => Game.Instance.RootUiContext.m_UIView as GlobalMapPCView;

        public NewGamePCView NewGamePCView => MainMenuPCView?.NewGamePCView;

        public LootPCView LootPCView => (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_LootContextPCView?.m_LootPCView;

        public LootCollectorPCView LootCollector => LootPCView?.m_Collector;

        public PartyPCView PartyPCView => InGamePCView?.m_StaticPartPCView?.m_PartyPCView ?? GlobalMapPCView?.m_PartyPCView;

        public SkipTimePCView SkipTimeView => InGamePCView?.m_StaticPartPCView?.m_SkipTimePCView ?? GlobalMapPCView?.m_SkipTimePCView;

        public RestPCView RestView => InGamePCView?.m_StaticPartPCView?.m_RestContextPCView?.m_RestPCView ?? GlobalMapPCView?.m_RestPCView;

        public GroupChangerPCView GroupChangerView => (InGamePCView?.m_StaticPartPCView?.m_GroupChangerContextPCView ?? GlobalMapPCView?.m_GroupChangerContextPCView)?.m_GroupChangerPCView;

        public VendorVM VendorViewVM => InGamePCView?.m_StaticPartPCView?.m_VendorPCView?.ViewModel;

        public SpellbookMemorizingPanelVM SpellbookMemorizingVM => (InGamePCView?.m_StaticPartPCView?.m_ServiceWindowsPCView ?? GlobalMapPCView.m_ServiceWindowsPCView)?.m_SpellbookPCView?.m_MemorizingPanelView?.ViewModel;

        public CharGenPCView CharGenView => (InGamePCView?.m_StaticPartPCView?.m_CharGenContextPCView ?? GlobalMapPCView?.m_CharGenContextPCView ?? MainMenuPCView?.m_CharGenContextPCView)?.m_CharGenPCView;

        public RespecWindowPCView RespecView => (InGamePCView?.m_StaticPartPCView?.m_CharGenContextPCView ?? GlobalMapPCView?.m_CharGenContextPCView)?.m_RespecWindowPCView;

        public InventoryVM InventoryVM => (Game.Instance.RootUiContext?.InGameVM?.StaticPartVM?.ServiceWindowsVM ?? Game.Instance.RootUiContext?.GlobalMapVM?.ServiceWindowsVM)?.InventoryVM?.Value;

    }
}
