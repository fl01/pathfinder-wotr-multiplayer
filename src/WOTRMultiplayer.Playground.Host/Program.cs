using System;
using System.IO;
using System.Linq;
using AutoMapper;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Config.DI;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Playground.Core;
using WOTRMultiplayer.Playground.Core.Dummies;
using WOTRMultiplayer.Services;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Playground.Host
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Playground")]
    public class Program
    {
        public static void Main(string[] args)
        {
            WellKnownSettings.Initialize();

            Console.WriteLine("Hello World!");
            Console.WriteLine("Press enter to host");
            Console.ReadLine();

            var serviceProvider = DIFactory.Create(new UnityModManagerSettings { UseDebugConsole = false }, "./");
            var host = new MultiplayerHost(
                serviceProvider.GetService<ILogger<MultiplayerHost>>(),
                new DummyGameInteractionService(),
                new DummyLevelingInteractionService(),
                new DummyPlayerNotificationService(),
                new DummyDialogInteractionService(),
                new DummyGlobalMapInteractionService(),
                new DummyPingInteractionService(),
                new DummyCombatInteractionService(),
                new MultiplayerSettingsProvider(new DummySettingsControllerAccessor()),
                serviceProvider.GetService<IFileSystemService>(),
                serviceProvider.GetService<INetworkServer>(),
                new DummyDiceRollStorage([new NetworkIntRollValue { Value = 66 }]),
                serviceProvider.GetService<IValueGenerator>(),
                serviceProvider.GetService<IMapper>());

            var saveGamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData\\LocalLow\\Owlcat Games\\Pathfinder Wrath Of The Righteous\\Saved Games\\Manual_34_FIRST_COMBAT.zks");
            var startUp = new NetworkGameStartUp(saveGamePath) { Characters = [], Title = "Playground Host Game Title" };
            host.Create(Guid.NewGuid().ToString(), startUp);

            var verbs = CommandLineHelper.LoadVerbs();
            Parser.Default.ParseArguments(["--help"], verbs);

            while (true)
            {
                var input = Console.ReadLine();
                var inputArgs = input.Split(' ').Select(x => x.Trim(' ')).ToList();
                Parser.Default
                    .ParseArguments(inputArgs, verbs)
                    .WithParsed(command => RunCommand(host, command, serviceProvider));
            }
        }

        private static void RunCommand(MultiplayerHost client, object command, IServiceProvider serviceProvider)
        {
            switch (command)
            {
                case CommandVerbs.ReadyCommandVerb:
                    client.ReadyChanged();
                    break;
                case CommandVerbs.AreaLoadedCommandVerb:
                    client.OnAreaLoaded();
                    break;
                case CommandVerbs.ExitCommandVerb:
                    Environment.Exit(0);
                    break;
                default:
                    break;
            }
        }
    }
}
