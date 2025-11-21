using System;

namespace WOTRMultiplayer.Localization.UnitTests
{
    public class WellKnownKeyTestCase
    {
        public string Name { get; set; }

        public Func<string> Key { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
