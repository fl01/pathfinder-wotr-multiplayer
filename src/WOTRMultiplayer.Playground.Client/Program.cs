using System;
using System.Linq;
using AutoMapper;
using CommandLine;
using Kingmaker.GameModes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Localization;
using WOTRMultiplayer.MP.Actors;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Playground.Core;
using WOTRMultiplayer.Playground.Core.Dummies;
using WOTRMultiplayer.Settings;

namespace WOTRMultiplayer.Playground.Client
{
    public class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Playground")]
        public static void Main(string[] args)
        {
            var serviceProvider = DI.DIFactory.Create(new UnityModManagerSettings { UseDebugConsole = false });
            var gameInteractionService = new DummyGameInteractionService();
            Console.WriteLine("Default save game dir=" + gameInteractionService.GetSaveGamePath());
            var client = new MultiplayerClient(
                serviceProvider.GetService<ILogger<MultiplayerClient>>(),
                gameInteractionService,
                serviceProvider.GetService<IIPEndPointParser>(),
                new MultiplayerSettingsProvider(new DummySettingsControllerAccessor()),
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkClient>(),
                new DummyDiceRollStorage([new NetworkIntRollValue { Value = 59 }]),
                serviceProvider.GetService<IValueGenerator>(),
                serviceProvider.GetService<IMapper>());

            var verbs = CommandLineHelper.LoadVerbs();
            Parser.Default.ParseArguments(["--help"], verbs).WithParsed(Console.WriteLine);

            while (true)
            {
                var input = Console.ReadLine();
                var inputArgs = input.Split(' ').Select(x => x.Trim(' ')).ToList();
                Parser.Default
                    .ParseArguments(inputArgs, verbs)
                    .WithParsed(command => RunCommand(client, command, serviceProvider));
            }
        }

        private static void RunCommand(MultiplayerClient client, object command, IServiceProvider serviceProvider)
        {
            switch (command)
            {
                case CommandVerbs.ReadyCommandVerb:
                    client.ReadyChanged();
                    break;
                case CommandVerbs.ClientLoadedCommandVerb:
                    client.OnAreaScenesLoaded();
                    break;
                case CommandVerbs.GameModeStartedCommandVerb gameModeStart:
                    var start = GameModeType.All.First(m => m.Index == (int)gameModeStart.GameModeTypeId);
                    client.OnStartGameMode(start);
                    break;
                case CommandVerbs.GameModeEndedCommandVerb gameModeEnd:
                    var end = GameModeType.All.First(m => m.Index == (int)gameModeEnd.GameModeTypeId);
                    client.OnStopGameMode(end);
                    break;
                case CommandVerbs.ShowRestViewCommandVerb showRestView:
                    client.OnShowRestView(showRestView.Phase);
                    break;
                case CommandVerbs.ConnectCommandVerb connect:
                    var result = client.Connect(connect.ServerAddress);
                    Console.WriteLine(result.MessageKey);
                    break;
                case CommandVerbs.ExitCommandVerb:
                    Environment.Exit(0);
                    break;
                case CommandVerbs.DialogWitnessCueCommandVerb dialog:
                    client.OnAfterCueShow(dialog.DialogName, dialog.Cue, dialog.HasSystemAnswer);
                    break;
                case CommandVerbs.DialogSuggestCueCommandVerb dialog:
                    client.Game.Dialog = new NetworkDialog(dialog.DialogName)
                    {
                        CurrentCueName = dialog.Cue
                    };
                    client.OnBeforeSelectDialogAnswer(dialog.DialogName, dialog.Cue, dialog.Answer, dialog.IsExitAnswer, dialog.ManualUnitSelectionId);
                    break;
                case CommandVerbs.DialogStartCommandVerb dialog:
                    client.StartDialog(dialog.DialogName, dialog.TargetUnitId, dialog.InitiatorUnitId, dialog.MapObjectId, dialog.SpeakerKey);
                    break;
                case CommandVerbs.CombatStartedCommandVerb:
                    client.CombatStarted();
                    break;
                case CommandVerbs.CombatRoundCommandVerb round:
                    client.CombatRoundStarted(round.Round);
                    break;
                case CommandVerbs.CombatTurnStartedCommandVerb turn:
                    client.OnBeforeStartTurn(turn.UnitId, turn.IsSurpriseRound);
                    break;
                case CommandVerbs.CombatTurnEndedCommandVerb turn:
                    client.OnBeforeEndTurn(turn.UnitId);
                    break;
                case CommandVerbs.EquipmentSlotUpdateCommandVerb equipment:
                    var slot = new MP.Entities.Equipment.NetworkEquipmentSlot
                    {
                        Item = new MP.Entities.Equipment.NetworkItem { UniqueId = equipment.ItemId },
                        OwnerId = equipment.UnitId,
                        Position = new MP.Entities.Equipment.NetworkEquipmentSlotPosition
                        {
                            Index = equipment.SlotIndex,
                            Type = equipment.SlotType
                        }
                    };
                    var character = new MP.Entities.NetworkCharacterOwnership { Name = equipment.UnitId, Owner = client.Game.Players.FirstOrDefault(), UnitId = equipment.UnitId };
                    client.Game.Characters.Add(character);
                    client.OnEquipmentSlotChanged(slot);
                    client.Game.Characters.Remove(character);
                    break;
                case CommandVerbs.EquipmentActiveHandSlotUpdateCommandVerb handSlot:
                    var set = new MP.Entities.Equipment.NetworkActiveHandEquipmentSet { Index = handSlot.SlotIndex, UnitId = handSlot.UnitId };
                    client.Game.Characters.FirstOrDefault().UnitId = set.UnitId;
                    client.OnChangeActiveHandEquipmentSet(set);
                    break;
                case CommandVerbs.DumpLocaleCommandVerb:
                    var localization = new LocalizationService(
                        serviceProvider.GetService<ILogger<LocalizationService>>(),
                        serviceProvider.GetService<IFileSystemService>(),
                        new DummyLocalizationManagerAccessor());
                    localization.UpdateLocale("dummy1");
                    break;
                default:
                    break;
            }
        }
    }
}
