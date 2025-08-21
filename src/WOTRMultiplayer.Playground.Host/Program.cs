using System;
using System.Collections.Generic;
using System.IO;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTR.Multiplayer.Playground.Core.Dummies;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.MP.Actors;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Playground.Host
{
    public class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Playground")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Playground")]
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Press enter to host");
            Console.ReadLine();

            var serviceProvider = DI.DIFactory.Create(new Config.UnityMod.UnityModManagerSettings { UseDebugConsole = false });
            var gameInteractionService = new DummyGameInteractionService();
            var host = new MultiplayerHost(
                serviceProvider.GetService<ILogger<MultiplayerHost>>(),
                gameInteractionService,
                serviceProvider.GetService<IMultiplayerSettingsProvider>(),
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkServer>(),
                new DummyDiceRollStorage([new NetworkIntRollValue { Value = 66 }]),
                serviceProvider.GetService<IValueGenerator>(),
                serviceProvider.GetService<IMapper>());
            //var characters = new List<NetworkCharacter> {
            //    new() { Name = "xdd", Portrait = "KitsuneFemaleRogue_Portrait"},
            //    new() { Name = "SeelahFemalePaladin_Portrait", Portrait = "SeelahFemalePaladin_Portrait"},
            //    new() { Name = "RegillMaleGnomeHellknight_Portrait", Portrait = "RegillMaleGnomeHellknight_Portrait"},
            //    new() { Name = "WenduagFemaleMongrelRanger_Portrait", Portrait = "WenduagFemaleMongrelRanger_Portrait"},
            //    new() { Name = "EmberFemaleElfWitch_Portrait", Portrait = "EmberFemaleElfWitch_Portrait"},
            //    new() { Name = "NenioFemaleKitsuneWizard_Portrait", Portrait = "NenioFemaleKitsuneWizard_Portrait"},
            //};
            var characters = new List<NetworkCharacterOwnership> {
                new() { Name = "Taolynn", Portrait = "KitsuneFemaleRogue_Portrait"}
                //new() { Name = "xdd", Portrait = "KitsuneFemaleRogue_Portrait"}
            };
            // Manual_33_DIALOGUE_SKILL_CHECK  - first cave
            // Manual_32_DIALOGUE - capital act3
            // Manual_34_FIRST_COMBAT - first combat in first cave
            var saveGamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\Manual_34_FIRST_COMBAT.zks");
            host.Create(saveGamePath, "1", characters);
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            ready - toggle host ready status
            owner_00 - change 0 char owner to 0 player
            owner_01 - change 0 char owner to 1 player
            start - start game
            move - move xdd to 22.92498, 42.053, -9.376869
            loaded - make host loaded
            pause - pause game
            unpause - unpause game
            leave-area - send leavearea notification 1b018b52-c1be-40bf-8937-1f2a77b96049
            dialog-answer_continue0001 - walk through first part of first dialog (until after cutscene)
            dialog-answer_continue0002 - walk through first part of first dialog (until after cutscene)
            dialog-answer_continue0003 - walk through first part of first dialog (until after cutscene)
            dialog-answer_continue0004 - walk through first part of first dialog (until after cutscene)
            dialog-answer_continue0005 - walk through first part of first dialog (until after cutscene)
            start-unit-dialog - Vendor_Quartermaster_Dialogue 2C1EE7 98fd05f4-4458-4d2d-97f6-752be49667c0
            start-dialog - MeetSeelahAnevia_Dialogue
            combat-started - send initialization message
            {Environment.NewLine}");

            const string DialogName = "MeetSeelahAnevia_Dialogue";

            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "ready":
                        host.ReadyChanged();
                        break;
                    case "owner_00":
                        host.ChangeCharacterOwner(0, 0);
                        break;
                    case "owner_01":
                        host.ChangeCharacterOwner(0, 1);
                        break;
                    case "start":
                        host.Start();
                        break;
                    case "move":
                        var move = new NetworkCharacterMove
                        {
                            UnitId = "xdd",
                            Destination = new NetworkVector3(22.92498f, 42.053f, -9.376869f),
                            Orientation = 138.3618f,
                            Delay = 0
                        };
                        host.MoveNonCombatCharacter(move);
                        break;
                    case "loaded":
                        host.OnAreaScenesLoaded();
                        break;
                    case "leave-area":
                        host.LeaveArea("1b018b52-c1be-40bf-8937-1f2a77b96049");
                        break;
                    case "dialog-answer_continue0001":
                        host.Game.Dialog = new NetworkDialog(DialogName)
                        {
                            Answer = new NetworkDialogAnswer
                            {
                                AnswerName = "DefaultContinue",
                                CueName = "Cue_0001",
                                ManualUnitSelectionId = null
                            }
                        };
                        host.SendSelectedAnswer();
                        break;
                    case "dialog-answer_continue0002":
                        host.Game.Dialog = new NetworkDialog(DialogName)
                        {
                            Answer = new NetworkDialogAnswer
                            {
                                AnswerName = "DefaultContinue",
                                CueName = "Cue_0002",
                                ManualUnitSelectionId = null
                            }
                        };
                        host.SendSelectedAnswer();
                        break;
                    case "dialog-answer_continue0003":
                        host.Game.Dialog = new NetworkDialog(DialogName)
                        {
                            Answer = new NetworkDialogAnswer
                            {
                                AnswerName = "DefaultContinue",
                                CueName = "Cue_0003",
                                ManualUnitSelectionId = null
                            }
                        };
                        host.SendSelectedAnswer();
                        break;
                    case "dialog-answer_continue0004":
                        host.Game.Dialog = new NetworkDialog(DialogName)
                        {
                            Answer = new NetworkDialogAnswer
                            {
                                AnswerName = "Answer_0042",
                                CueName = "Cue_0004",
                                ManualUnitSelectionId = null
                            }
                        };
                        host.SendSelectedAnswer();
                        break;
                    case "dialog-answer_continue0005":
                        host.Game.Dialog = new NetworkDialog(DialogName)
                        {
                            Answer = new NetworkDialogAnswer
                            {
                                AnswerName = "DefaultContinue",
                                CueName = "Cue_0044",
                                ManualUnitSelectionId = null
                            }
                        };
                        host.SendSelectedAnswer();
                        break;
                    case "start-unit-dialog":
                        host.StartDialog("Vendor_Quartermaster_Dialogue", "2C1EE7", "98fd05f4-4458-4d2d-97f6-752be49667c0", null, null);
                        break;
                    case "start-dialog":
                        host.StartDialog("MeetSeelahAnevia_Dialogue", null, null, null, null);
                        break;
                    case "combat-started":
                        host.CombatStarted();
                        host.CombatRoundStarted(1);
                        host.CanContinueCombat();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
