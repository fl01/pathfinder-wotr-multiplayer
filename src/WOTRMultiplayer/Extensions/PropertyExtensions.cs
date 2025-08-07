using System;
using System.Reflection;

namespace WOTRMultiplayer.Extensions
{
    public static class PropertyInfoExtensions
    {
        public static void SetPropertyValue(this PropertyInfo prop, object targetObj, object value)
        {
            const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                setter.Invoke(targetObj, [value]);
            }
            else
            {
                var backingField = prop.DeclaringType.GetField($"<{prop.Name}>k__BackingField", DeclaredOnlyLookup)
                    ?? throw new InvalidOperationException($"Could not find a way to set {prop.DeclaringType.FullName}.{prop.Name}. Try adding a private setter.");

                backingField.SetValue(targetObj, value);
            }
        }
    }
}
