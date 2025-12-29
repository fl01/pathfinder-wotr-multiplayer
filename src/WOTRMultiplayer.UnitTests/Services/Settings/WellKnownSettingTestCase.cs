using System;

namespace WOTRMultiplayer.UnitTests.Services.Settings
{
    public class WellKnownSettingTestCase
    {
        public string Name { get; set; }

        public Func<string> Key { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
