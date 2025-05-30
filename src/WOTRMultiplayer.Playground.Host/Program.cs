using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP;

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
            var host = serviceProvider.GetService<IMultiplayerHost>();
            var portraits = new List<string> {
                "KitsuneFemaleRogue_Portrait","SeelahFemalePaladin_Portrait", "RegillMaleGnomeHellknight_Portrait",
                "WenduagFemaleMongrelRanger_Portrait","EmberFemaleElfWitch_Portrait","NenioFemaleKitsuneWizard_Portrait"
            };
            host.Create(string.Empty, portraits, new MultiplayerSettings());
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
