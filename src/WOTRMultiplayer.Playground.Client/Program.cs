using System;
using System.IO;
using Kingmaker.EntitySystem.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Saves;
using WOTRMultiplayer.MP;
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
            var client = new MultiplayerClient(
                serviceProvider.GetService<ILogger<MultiplayerClient>>(),
                serviceProvider.GetService<IGameInteractionService>(),
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
            {Environment.NewLine}");
            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "ready":
                        client.ReadyChanged();
                        break;
                    default:
                        break;
                }
            }
        }

        public class DummySaveGameService : ISaveGameService
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
    }
}
