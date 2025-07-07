using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Saves;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;
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
            var unityPathService = new DummySaveGameService();
            Console.WriteLine("Default save game dir=" + unityPathService.GetSaveGamePath());
            Console.WriteLine("Press enter to join");
            Console.ReadLine();
            var gameInteractionService = new DummyGameInteractionService();
            var client = new MultiplayerClient(
                serviceProvider.GetService<ILogger<MultiplayerClient>>(),
                gameInteractionService,
                serviceProvider.GetService<IIPEndPointParser>(),
                serviceProvider.GetService<IMultiplayerSettingsProvider>(),
                unityPathService,
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkServerClient>());
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
                    default:
                        break;
                }
            }
        }

        private class DummySaveGameService : ISaveGameService
        {
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
        }

        private class DummyGameInteractionService : IGameInteractionService
        {
            public bool IsPaused { get; set; }

            public List<NetworkCharacter> GetPartyPlayers()
            {
                return [];
            }

            public void LeaveArea(string areaExitId)
            {
            }

            public void MarkSuggestedDialogAnswers(List<NetworkDialogAnswerSuggestion> suggestions)
            {
            }

            public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
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
        }
    }
}
