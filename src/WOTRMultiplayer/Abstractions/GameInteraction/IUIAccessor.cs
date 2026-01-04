using Kingmaker.UI.MVVM._PCView.CharGen;
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

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IUIAccessor
    {
        EscMenuPCView EscMenu { get; }

        GlobalMapPCView GlobalMapPCView { get; }

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
    }
}
