using CommandLine;

namespace WOTRMultiplayer.Playground.Host
{
    /// <summary>
    /// verbs are listed in the same order
    /// </summary>
    public class CommandVerbs
    {
        [Verb("ready", HelpText = "triggers ready status change")]
        public class ReadyCommandVerb
        {
        }

        [Verb("loaded", HelpText = "send gameloaded event")]
        public class AreaLoadedCommandVerb
        {
        }

        [Verb("exit", HelpText = "cya next time")]
        public class ExitCommandVerb
        {
        }
    }
}
