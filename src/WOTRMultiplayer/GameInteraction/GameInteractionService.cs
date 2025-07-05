using System;
using System.Linq;
using System.Numerics;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using Kingmaker.UI.MVVM._PCView.InGame;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly ILogger<GameInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;

        public GameInteractionService(
            ILogger<GameInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
        }

        public bool IsPaused => Game.Instance.IsPaused;

        public void LeaveArea(string areaExitId)
        {
            _logger.LogInformation("Leaving area. AreaExitId={areaExitId}", areaExitId);
            _mainThreadAccessor.Enqueue(() =>
            {
                var allTransitions = Game.Instance.State.MapObjects.All.Select(o => o.View.GetComponent<AreaTransition>()).Where(t => t != null).ToList();
                var transition = allTransitions.FirstOrDefault(x => string.Equals(x.GetComponent<MapObjectView>().UniqueId, areaExitId, System.StringComparison.OrdinalIgnoreCase));
                var areaTransition = transition?.GetComponent<MapObjectView>()?.Data.Get<AreaTransitionPart>();
                if (areaTransition == null)
                {
                    _logger.LogError("Unable to find requested area transition. TransitionsCount={transitionsCount}, AreaExitId={areaExitId}", allTransitions.Count, areaExitId);
                    return;
                }

                // AreaTransitionGroupCommand.ExecuteTransition
                if (Game.Instance.State.LoadedAreaState.Encounter == null && areaTransition.AreaEnterPoint.Area.IsGlobalMap)
                {
                    BlueprintGlobalMap globalMap = BlueprintRoot.Instance.GlobalMap.GetGlobalMap(areaTransition.AreaEnterPoint);
                    if (globalMap != null)
                    {
                        Game.Instance.Player.GetGlobalMap(globalMap).Player.AreaReturnPoint = areaTransition.GetEnterPointToReturnTo();
                    }
                }

                Game.Instance.LoadArea(areaTransition.AreaEnterPoint, areaTransition.Settings.AutoSaveMode, null);
            });

        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.CharacterName, characterName));
            if (character == null)
            {
                _logger.LogError("Can't find character. Name={characterName}", characterName);
                return;
            }

            var unityDestination = new UnityEngine.Vector3(destination.X, destination.Y, destination.Z);
            var command = new UnitMoveTo(unityDestination, 0.3f)
            {
                MovementDelay = delay,
                Orientation = orientation,
                CreatedByPlayer = true
            };
            character.Commands.Run(command);
        }

        public void Pause(bool isPaused)
        {
            _logger.LogInformation("Pause game. Value={isPaused}", isPaused);
            if (isPaused)
            {
                Game.Instance.StartMode(GameModeType.Pause);
                return;
            }

            Game.Instance.StopMode(GameModeType.Pause);
        }

        public void SetDialogContinueButtonState(bool isEnabled)
        {
            const string NextOrEndBindingName = "NextOrEnd";
            try
            {
                var dialogView = (Game.Instance.RootUiContext.m_UIView as InGamePCView)?.m_StaticPartPCView?.m_DialogContextPCView;
                var systemButtonGameObject = dialogView?.m_DialogPCView?.gameObject.transform.Find("Body/SystemButton");
                var continueButton = systemButtonGameObject?.GetComponent<OwlcatButton>();
                if (continueButton == null)
                {
                    _logger.LogError("Continue button - unable to find");
                    return;
                }

                continueButton.Interactable = isEnabled;
                _logger.LogInformation("Continue button - state changed. State={state}", isEnabled);

                if (Game.Instance.Keyboard.m_BindingCallbacks.TryGetValue(NextOrEndBindingName, out var callbacks))
                {
                    static bool hasConfiguredCallback(Action x) => x.Target is DialogSystemAnswerPCView or GameInteractionService;

                    if (isEnabled && !callbacks.Any(hasConfiguredCallback))
                    {
                        Game.Instance.Keyboard.Bind(NextOrEndBindingName, continueButton.OnLeftClick.Invoke);
                        _logger.LogInformation("Continue button - hotkeys have been enabled");
                    }
                    else if (!isEnabled)
                    {
                        callbacks.RemoveAll(hasConfiguredCallback);
                        _logger.LogInformation("Continue button - hotkeys have been disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Continue button - unable to change state due to error");
                throw;
            }
        }
    }
}
