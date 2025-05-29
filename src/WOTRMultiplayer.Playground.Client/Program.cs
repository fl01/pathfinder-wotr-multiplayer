using System;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Abstractions.MP;

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
            Console.WriteLine("Press enter to join");
            Console.ReadLine();


            var client = serviceProvider.GetService<IMultiplayerClient>();
            client.Join("127.0.0.1:1024", new MP.MultiplayerSettings { PlayerName = "hello" });
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            {Environment.NewLine}");
            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "1":
                        client.ReadyChanged();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
