using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace WOTRMultiplayer.Localization
{
    public static class WellKnownKeysInitializer
    {
        public const string KeyPathSeparator = ".";
        public const string RootKey = "wotrmultiplayer";

        public static void Run()
        {
            var typesToProcess = new Stack<(string, Type)>();
            var rootType = typeof(WellKnownKeys);
            var rootPath = rootType.GetCustomAttribute<DescriptionAttribute>().Description;
            typesToProcess.Push((rootPath, rootType));
            while (typesToProcess.Count > 0)
            {
                var (currentPath, currentType) = typesToProcess.Pop();
                var children = currentType.GetNestedTypes().Where(n => n.IsClass && n.GetCustomAttribute<DescriptionAttribute>() != null).ToList();
                if (children.Count > 0)
                {
                    foreach (var child in children)
                    {
                        var childPath = string.Join(KeyPathSeparator, currentPath, child.GetCustomAttribute<DescriptionAttribute>().Description);
                        typesToProcess.Push((childPath, child));
                    }

                    continue;
                }

                var keyProperty = currentType.GetProperty("Key")
                    ?? throw new InvalidOperationException($"A well-known key type has neither children nor Key property. Type={currentType}");

                keyProperty.SetValue(null, currentPath);
            }
        }
    }
}
