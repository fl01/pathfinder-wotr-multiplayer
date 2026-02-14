using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.UI;
using Kingmaker.UI.Models.Log;
using Kingmaker.UI.Models.Log.CombatLog_ThreadSystem;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Tooltips;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog.Tooltips;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class PlayerNotificationService : IPlayerNotificationService
    {
        private readonly ILogger<PlayerNotificationService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;

        public PlayerNotificationService(
            ILogger<PlayerNotificationService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IGameStateLookupService gameStateLookupService)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _gameStateLookupService = gameStateLookupService;
        }

        public void ShowModalMessage(string messageKey, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var message = GetLocalizedText(messageKey, args);
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message, MessageModalBase.ModalType.Message, null));
            });
        }

        public void ShowWarningNotification(string messageKey, bool addToLog, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var message = GetLocalizedText(messageKey, args);
                EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, addToLog));
            });
        }

        public void AddCombatText(string messageKey, CombatTextSeverity combatTextSeverity, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                AddCombatText(messageKey, combatTextSeverity, template: null, args);
            });
        }

        public void AddCombatText(string messageKey, CombatTextSeverity combatTextSeverity, AbilityTooltipLog abilityTooltipLog, params object[] args)
        {
            _mainThreadAccessor.Post(() =>
            {
                var template = new TooltipTemplateAbility(abilityTooltipLog.AbilityData);
                AddCombatText(messageKey, combatTextSeverity, template: template, args);
            });
        }

        private void AddCombatText(string messageKey, CombatTextSeverity combatTextSeverity, TooltipBaseTemplate template, params object[] args)
        {
            var parameters = GetCombatLogParameters(args).ToArray();
            var message = GetLocalizedText(messageKey, parameters);
            var color = GetTextColor(combatTextSeverity);
            var combatLogMessage = new CombatLogMessage(message, color, PrefixIcon.None, template, false);
            Game.Instance.RootUiContext.InGameVM?.StaticPartVM?.CombatLogVM?.AddNewMessage(combatLogMessage);
        }

        private Color32 GetTextColor(CombatTextSeverity combatTextSeverity)
        {
            return combatTextSeverity switch
            {
                CombatTextSeverity.Critical => Color.red,
                CombatTextSeverity.Common => GameLogStrings.Instance.DefaultColor,
                _ or CombatTextSeverity.Debug => LogThreadBase.Colors.WarningLogColor,
            };
        }

        private IEnumerable<object> GetCombatLogParameters(params object[] args)
        {
            foreach (var param in args)
            {
                if (param is UnitEntityLog unit)
                {
                    yield return GetUnitName(unit);
                    continue;
                }

                yield return param;
            }
        }

        public void AddCombatText(RulebookEvent rulebookEvent)
        {
            _mainThreadAccessor.Post(() =>
            {
                var logEvent = GameLogEventsFactory.Create(rulebookEvent);
                if (logEvent == null)
                {
                    _logger.LogWarning("Unable to create combat log event for specified rule. RuleType={RuleType}", rulebookEvent?.GetType().Name);
                    return;
                }

                Game.Instance.GameLogController.AddReadyEvent(logEvent);
            });
        }

        private string GetLocalizedText(string messageKey, params object[] args)
        {
            var localizedPart = new LocalizedString { Key = messageKey };
            var message = string.Format(localizedPart, args);
            return message;
        }

        private string GetUnitName(UnitEntityLog unitLog)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitLog.UnitId);
            if (unit == null)
            {
                return unitLog.UnitId;
            }

            string textColor = ColorUtility.ToHtmlStringRGB(unit.Blueprint.Color);
            var unitName = $"<b><color=#{textColor}>{unit.CharacterName}</color></b> ({unit.UniqueId})";
            return unitName;
        }
    }
}
