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
using Kingmaker.UI.MVVM._PCView.Kingdom;
using Kingmaker.UI.MVVM._PCView.Kingdom.KingdomInfo;
using Kingmaker.UI.MVVM._PCView.Loot;
using Kingmaker.UI.MVVM._PCView.NewGame;
using Kingmaker.UI.MVVM._PCView.Party;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._PCView.TacticalCombat.Result;
using Kingmaker.UI.MVVM._VM.ServiceWindows;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Inventory;
using Kingmaker.UI.MVVM._VM.ServiceWindows.Spellbook.MemorizingPanel;
using Kingmaker.UI.MVVM._VM.Vendor;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IUIAccessor
    {
        EscMenuPCView EscMenu { get; }

        GlobalMapPCView GlobalMapPCView { get; }

        KingdomPCView KingdomPCView { get; }

        CityBuilderPCView CityBuilderPCView { get; }

        CommonPCView CommonPCView { get; }

        ServiceWindowsVM ServiceWindowsVM { get; }

        NewGamePCView NewGamePCView { get; }

        LootPCView LootPCView { get; }

        LootCollectorPCView LootCollector { get; }

        PartyPCView PartyPCView { get; }

        SkipTimePCView SkipTimeView { get; }

        RestPCView RestView { get; }

        GroupChangerPCView GroupChangerView { get; }

        VendorVM VendorViewVM { get; }

        SpellbookMemorizingPanelVM SpellbookMemorizingVM { get; }

        CharGenPCView CharGenView { get; }

        RespecWindowPCView RespecView { get; }

        InventoryVM InventoryVM { get; }

        CombatLogPCView CombatLogPCView { get; }

        DialogContextPCView DialogContextPCView { get; }

        TacticalCombatResultsPCView TacticalCombatResultsPCView { get; }

        GlobalMapToolbarPCView GlobalMapToolbarPCView { get; }
        GlobalMapCrusadeArmiesPCView GlobalMapCrusadeArmiesPCView { get; }
        ArmyCartBuyLeaderPCView ArmyCartBuyLeaderPCView { get; }
        ArmyInfoHUDPCView ArmyInfoHUDPCView { get; }
        ArmyInfoPCView ArmyInfoPCView { get; }
        KingdomInfoPCView KingdomInfoPCView { get; }
        GlobalMapMenuPCView GlobalMapMenuPCView { get; }
        GlobalMapArmyPointerMarkerPCView GlobalMapArmyPointerMarkerPCView { get; }
        GlobalMapEnterMessagePCView GlobalMapEnterMessagePCView { get; }
        RecruitPCView RecruitPCView { get; }
        CombatResultPCView CombatResultPCView { get; }
        LeaderLevelUpPCView LeaderLevelUpPCView { get; }

        void CloseAllWindows();
    }
}
