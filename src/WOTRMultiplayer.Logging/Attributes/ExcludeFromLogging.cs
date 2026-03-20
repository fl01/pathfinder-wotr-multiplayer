using System;

namespace WOTRMultiplayer.Logging.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ExcludeFromLogging : Attribute
    {
    }
}
