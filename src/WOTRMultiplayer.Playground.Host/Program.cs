using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker.EntitySystem.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.MP;
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
            var host = new MultiplayerHost(
                serviceProvider.GetService<ILogger<MultiplayerHost>>(),
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkServer>());
            var portraits = new List<string> {
                "KitsuneFemaleRogue_Portrait","SeelahFemalePaladin_Portrait", "RegillMaleGnomeHellknight_Portrait",
                "WenduagFemaleMongrelRanger_Portrait","EmberFemaleElfWitch_Portrait","NenioFemaleKitsuneWizard_Portrait"
            };
            var saveGamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\Quick_12.zks");
            var save = new SaveInfo
            {
                FolderName = saveGamePath,
            };
            host.Create(save, portraits, new MultiplayerSettings());
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            ready - toggle host ready status
            owner_00 - change 0 char owner to 0 player
            owner_01 - change 0 char owner to 1 player
            start - start game
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
                    default:
                        break;
                }
            }
        }
    }
}
