using System;

namespace WOTRMultiplayer.Logging.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsComplexType(this Type type)
        {
            if (type.IsPrimitive)
            {
                return false;
            }

            if (type.IsEnum)
            {
                return false;
            }

            if (type == typeof(string))
            {
                return false;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                return false;
            }

            return true;
        }
    }
}
