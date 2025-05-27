using System;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Abstractions.MP;

namespace WOTRMultiplayer.Playground.Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var serviceProvider = DI.DIFactory.Create(new Config.UnityMod.UnityModManagerSettings { UseDebugConsole = false });
            Console.WriteLine("Press enter to join");
            Console.ReadLine();


            var client = serviceProvider.GetService<IMultiplayerClient>();
            client.Join("127.0.0.1:1024", null);
            var input = string.Empty;

            Console.Write(@$"
            exit - exit the program
            {Environment.NewLine}");
            while ((input = Console.ReadLine()) != "exit")
            {
                switch (input)
                {
                    case "1":
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
