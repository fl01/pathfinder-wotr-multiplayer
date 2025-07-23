using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Playground.Client
{
    public class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Playground")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Playground")]
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var serviceProvider = DI.DIFactory.Create(new Config.UnityMod.UnityModManagerSettings { UseDebugConsole = false });
            var gameInteractionService = new DummyGameInteractionService();
            Console.WriteLine("Default save game dir=" + gameInteractionService.GetSaveGamePath());
            Console.WriteLine("Press enter to join");
            Console.ReadLine();
            var client = new MultiplayerClient(
                serviceProvider.GetService<ILogger<MultiplayerClient>>(),
                gameInteractionService,
                serviceProvider.GetService<IIPEndPointParser>(),
                serviceProvider.GetService<IMultiplayerSettingsProvider>(),
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkServerClient>(),
                new DummyDiceRollStorage(),
                serviceProvider.GetService<IMapper>());
            client.Connect("127.0.0.1:1024");
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            ready - toggle client ready status
            loaded - send gameloaded
            pause - pause game
            unpause - unpause game
            dialog-witness-cue_0001 - witness 1 cue of MeetSeelahAnevia_Dialogue
            dialog-witness-cue_0002 - witness 2 cue of MeetSeelahAnevia_Dialogue
            dialog-witness-cue_0003 - witness 3 cue of MeetSeelahAnevia_Dialogue
            dialog-suggest-cue_0004_2 - suggest option 2 on 4 cue
            dialog-suggest-cue_0004_3 - suggest option 3 on 4 cue
            start-unit-dialog - Vendor_Quartermaster_Dialogue 2C1EE7 98fd05f4-4458-4d2d-97f6-752be49667c0

            {Environment.NewLine}");
            const string DialogName = "MeetSeelahAnevia_Dialogue";

            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "ready":
                        client.ReadyChanged();
                        break;
                    case "loaded":
                        client.GameLoaded();
                        break;
                    case "pause":
                        client.Pause();
                        break;
                    case "unpause":
                        client.Unpause();
                        break;
                    case "dialog-witness-cue_0001":
                        client.OnAfterCueShow(DialogName, "Cue_0001", false);
                        break;
                    case "dialog-witness-cue_0002":
                        client.OnAfterCueShow(DialogName, "Cue_0002", false);
                        break;
                    case "dialog-witness-cue_0003":
                        client.OnAfterCueShow(DialogName, "Cue_0003", false);
                        break;
                    case "dialog-suggest-cue_0004_2":
                        client.CurrentGame.Dialog = new NetworkDialog(DialogName)
                        {
                            CurrentCueName = "Cue_0004"
                        };
                        client.OnBeforeSelectDialogAnswer(DialogName, "Cue_0004", "Answer_0007", false, null);
                        break;
                    case "dialog-suggest-cue_0004_3":
                        client.CurrentGame.Dialog = new NetworkDialog(DialogName)
                        {
                            CurrentCueName = "Cue_0004"
                        };
                        client.OnBeforeSelectDialogAnswer(DialogName, "Cue_0004", "Answer_0042", false, null);
                        break;
                    case "start-unit-dialog":
                        client.StartDialog("Vendor_Quartermaster_Dialogue", "2C1EE7", "98fd05f4-4458-4d2d-97f6-752be49667c0", null, null);
                        break;
                    case "combat-started":
                        client.CombatStarted();
                        break;
                    case "combat-round-1":
                        client.CombatRoundStarted(1);
                        break;
                    case "combat-round-2":
                        client.CombatRoundStarted(2);
                        break;
                    case "combat-round-3":
                        client.CombatRoundStarted(3);
                        break;
                    case "combat-turn-started-camellia":
                        client.OnBeforeStartTurn("3996", false);
                        break;
                    case "combat-turn-ended-camellia":
                        client.OnBeforeEndTurn("3996");
                        break;
                    case "combat-turn-started-main":
                        client.OnBeforeStartTurn("a950ad75-65cd-4dc1-96e9-444e291fed7e", false);
                        break;
                    case "combat-turn-ended-main":
                        client.OnBeforeEndTurn("a950ad75-65cd-4dc1-96e9-444e291fed7e");
                        break;
                    case "combat-turn-started-seelah":
                        client.OnBeforeStartTurn("38AF", false);
                        break;
                    case "combat-turn-ended-seelah":
                        client.OnBeforeEndTurn("38AF");
                        break;
                    case "combat-turn-started-3502":
                        client.OnBeforeStartTurn("3502", false);
                        break;
                    case "combat-turn-ended-3502":
                        client.OnBeforeEndTurn("3502");
                        break;
                    case "combat-turn-started-3521":
                        client.OnBeforeStartTurn("3521", false);
                        break;
                    case "combat-turn-ended-3521":
                        client.OnBeforeEndTurn("3521");
                        break;
                    case "combat-turn-started-3560":
                        client.OnBeforeStartTurn("3560", false);
                        break;
                    case "combat-turn-ended-3560":
                        client.OnBeforeEndTurn("3560");
                        break;
                    default:
                        break;
                }
            }
        }

        private class DummyGameInteractionService : IGameInteractionService
        {
            public bool IsPaused { get; set; }

            public string GetSaveGamePath()
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var fullPath = Path.Combine(appData, "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\");
                return fullPath;
            }

            public SaveInfo LoadSave(string path)
            {
                return null;
            }

            public bool IsUnitAI(string unitId)
            {
                return true;
            }

            public List<NetworkCharacterOwnership> GetPartyPlayers()
            {
                return [];
            }

            public List<NetworkUnit> GetUnitsInCombat()
            {
                return [];
            }

            public void LeaveArea(string areaExitId)
            {
            }

            public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
            {
            }

            public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
            {
            }

            public void Pause(bool isPaused)
            {
            }

            public void PlaySound(UISoundType type)
            {
            }

            public void SelectDialogAnswer(string dialogName, string cueName, string answerName, string manualUnitSelectionId)
            {
            }

            public void SetDialogContinueButtonState(bool isEnabled)
            {
            }

            public void ShowModalMessage(string error)
            {
            }

            public Task<bool> StartDialogAsync(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
            {
                return Task.FromResult(true);
            }

            public void QuickLoadGame(string savePath)
            {
            }

            public void LoadGameFromMainMenu(string savePath)
            {
            }

            public string GetPetOwnerId(string unitId)
            {
                return null;
            }

            public void StartTurnBasedCombatTurn(bool isActingInSurpriseRound)
            {
            }

            public void EndTurnBasedCombatTurn()
            {
            }

            public Task UpdateUnitsPositionAsync(List<NetworkUnit> networkUnits)
            {
                return Task.CompletedTask;
            }

            public void ClickUnitInCombat(NetworkClick click)
            {
            }

            public void ClickGroundInCombat(NetworkClick click)
            {
            }

            public void ClickAbilityInCombat(NetworkClick click)
            {
            }

            public bool CombatTurnHasBeenFinished()
            {
                return true;
            }
        }

        private class DummyDiceRollStorage : IDiceRollStorage
        {
            public bool Save(NetworkDiceRoll rollDice)
            {
                return true;
            }

            public NetworkDiceRoll Get(int rollId, long playerId, bool ensureCompleted = true)
            {
                return new NetworkDiceRoll
                {
                    Result = 55
                };
            }

            public int GetUniqueId(NetworkDiceRoll roll)
            {
                return -1;
            }

            public void Reset()
            {
            }

            public void Reset<T>()
                where T : NetworkDiceRoll
            {
            }

            public Task<NetworkDiceRoll> GetAsync(int rollId, long playerId, TimeSpan? timeout)
            {
                return Task.FromResult(Get(rollId, playerId));
            }
        }
    }
}
