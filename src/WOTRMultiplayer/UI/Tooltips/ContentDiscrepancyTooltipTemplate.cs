using System.Collections.Generic;
using System.Linq;
using Kingmaker.Localization;
using Kingmaker.UI.MVVM._VM.Tooltip.Bricks;
using Owlcat.Runtime.UI.Tooltips;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Content;

namespace WOTRMultiplayer.UI.Tooltips
{
    public class ContentDiscrepancyTooltipTemplate : TooltipBaseTemplate
    {
        private readonly NetworkPlayer _player;

        public ContentDiscrepancyTooltipTemplate(NetworkPlayer player)
        {
            _player = player;
        }

        public override IEnumerable<ITooltipBrick> GetHeader(TooltipTemplateType type)
        {
            yield return new TooltipBrickTitle(string.Format(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Title.Key }, _player.Name));
        }

        public override IEnumerable<ITooltipBrick> GetBody(TooltipTemplateType type)
        {
            if (_player.ContentState.DiscrepantDLCs.Any())
            {
                yield return new TooltipBrickTitle(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.DLCs.Title.Key }, TooltipTitleType.H1, saberFormat: true);
                var byReason = _player.ContentState.DiscrepantDLCs.GroupBy(x => x.Reason).OrderBy(x => x.Key);
                foreach (var reasonGroup in byReason)
                {
                    yield return new TooltipBrickTitle(GetDLCDiscrepancyReasonText(reasonGroup.Key), TooltipTitleType.H4);
                    foreach (var discrepancy in reasonGroup.OrderBy(x => x.DLC.Id))
                    {
                        yield return new TooltipBrickText(discrepancy.DLC.FullName);
                    }
                }
            }

            if (_player.ContentState.DiscrepantDLCs.Any() && _player.ContentState.DiscrepantMods.Any())
            {
                yield return new TooltipBrickSpace();
                yield return new TooltipBrickSeparator(TooltipBrickElementType.Medium);
                yield return new TooltipBrickSpace();
            }

            if (_player.ContentState.DiscrepantMods.Any())
            {
                yield return new TooltipBrickTitle(new LocalizedString { Key = WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Mods.Title.Key }, TooltipTitleType.H1, saberFormat: true);
                var byReason = _player.ContentState.DiscrepantMods.GroupBy(x => x.Reason).OrderBy(x => x.Key);
                foreach (var reasonGroup in byReason)
                {
                    yield return new TooltipBrickTitle(GetModDiscrepancyReasonText(reasonGroup.Key), TooltipTitleType.H4);
                    foreach (var discrepancy in reasonGroup.OrderBy(x => x.Mod.Id))
                    {
                        yield return new TooltipBrickText(discrepancy.Mod.FullName);
                    }
                }
            }
        }

        private string GetDLCDiscrepancyReasonText(NetworkDiscrepancyReason reason)
        {
            var key = reason switch
            {
                NetworkDiscrepancyReason.Missing => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.DLCs.Reasons.Missing.Key,
                NetworkDiscrepancyReason.Extra => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.DLCs.Reasons.Extra.Key,
                _ => null
            };

            return string.IsNullOrEmpty(key) ? reason.ToString() : new LocalizedString { Key = key };
        }

        private string GetModDiscrepancyReasonText(NetworkDiscrepancyReason reason)
        {
            var key = reason switch
            {
                NetworkDiscrepancyReason.Missing => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Mods.Reasons.Missing.Key,
                NetworkDiscrepancyReason.Extra => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Mods.Reasons.Extra.Key,
                NetworkDiscrepancyReason.Disabled => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Mods.Reasons.Disabled.Key,
                NetworkDiscrepancyReason.VersionMismatch => WellKnownKeys.LobbyWindow.Tooltips.ContentDiscrepancy.Mods.Reasons.VersionMismatch.Key,
                _ => null
            };

            return string.IsNullOrEmpty(key) ? reason.ToString() : new LocalizedString { Key = key };
        }
    }
}
