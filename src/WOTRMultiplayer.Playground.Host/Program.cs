using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Kingmaker.EntitySystem.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls;
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
                new DummyRollStorage());
            //var characters = new List<NetworkCharacter> {
            //    new() { Name = "xdd", Portrait = "KitsuneFemaleRogue_Portrait"},
            //    new() { Name = "SeelahFemalePaladin_Portrait", Portrait = "SeelahFemalePaladin_Portrait"},
            //    new() { Name = "RegillMaleGnomeHellknight_Portrait", Portrait = "RegillMaleGnomeHellknight_Portrait"},
            //    new() { Name = "WenduagFemaleMongrelRanger_Portrait", Portrait = "WenduagFemaleMongrelRanger_Portrait"},
            //    new() { Name = "EmberFemaleElfWitch_Portrait", Portrait = "EmberFemaleElfWitch_Portrait"},
            //    new() { Name = "NenioFemaleKitsuneWizard_Portrait", Portrait = "NenioFemaleKitsuneWizard_Portrait"},
            //};
            var characters = new List<NetworkCharacter> {
                new() { Name = "Taolynn", Portrait = "KitsuneFemaleRogue_Portrait"}
            };
            var saveGamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\Manual_33_DIALOGUE_SKILL_CHECK.zks");
            var save = new SaveInfo
            {
                FolderName = saveGamePath,
            };
            host.Create(save, characters);
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
            {Environment.NewLine}");
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
                        host.MoveCharacter("xdd", new System.Numerics.Vector3(22.92498f, 42.053f, -9.376869f), 0, 138.3618f);
                        break;
                    case "loaded":
                        host.GameLoaded();
                        host.Unpause();
                        break;
                    case "pause":
                        host.Pause();
                        break;
                    case "unpause":
                        host.Unpause();
                        break;
                    case "leave-area":
                        host.LeaveArea("1b018b52-c1be-40bf-8937-1f2a77b96049");
                        break;
                    default:
                        break;
                }
            }
        }

        private class DummyGameInteractionService : IGameInteractionService
        {
            public bool IsPaused { get; set; }

            public void LeaveArea(string areaExitId)
            {
            }

            public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
            {
            }

            public void Pause(bool isPaused)
            {
            }
        }

        private class DummyRollStorage : IRollStorage
        {
            public void Add(RollDice rollDice)
            {
            }

            public RollDice Get(int rollId, int playerId)
            {
                if (rollId == 1469590275)
                {
                    return new RollDice
                    {
                        Result = 25
                    };
                }

                return new RollDice
                {
                    Result = 99
                };
            }

            public int GetUniqueId(RollDice roll)
            {
                return -1;
            }

            public void Reset()
            {
            }
        }
    }
}
