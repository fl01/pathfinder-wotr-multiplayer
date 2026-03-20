using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Logging.Extensions;

namespace WOTRMultiplayer.Logging.Object
{
    public static class ObjectLoggingMetadata
    {
        private static Dictionary<Type, List<ObjectPropertyPath>> _metadata = [];

        public static void Initialize(IEnumerable<Type> types)
        {
            _metadata = Build(_metadata, types);
        }

        public static Dictionary<string, object> GetLoggingInfo(object message)
        {
            var result = new Dictionary<string, object>();
            var type = message.GetType();
            if (type == null || !_metadata.TryGetValue(type, out var typeMetadata))
            {
                return null;
            }

            foreach (var metadata in typeMetadata)
            {
                var value = metadata.GetValue(message);
                if (value == null)
                {
                    result.Add(metadata.ToString(), null);
                    continue;
                }
                switch (value)
                {
                    case byte[] array:
                        result.Add(metadata.ToString(), array.Length);
                        break;
                    default:
                        result.Add(metadata.ToString(), value);
                        break;
                }
            }

            return result;
        }

        private static Dictionary<Type, List<ObjectPropertyPath>> Build(Dictionary<Type, List<ObjectPropertyPath>> result, IEnumerable<Type> types)
        {
            var processedRoots = new HashSet<Type>();

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<ExcludeFromLogging>() != null)
                {
                    continue;
                }

                if (!processedRoots.Add(type))
                {
                    continue;
                }

                result[type] = BuildForType(type);
            }

            return result;
        }

        private static List<ObjectPropertyPath> BuildForType(Type rootType)
        {
            var results = new List<ObjectPropertyPath>();

            var queue = new Queue<(Type type, List<PropertyInfo> path)>();
            queue.Enqueue((rootType, new List<PropertyInfo>()));
            while (queue.Count > 0)
            {
                var (currentType, currentPath) = queue.Dequeue();
                var properties = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
                foreach (var property in properties)
                {
                    var newPath = new List<PropertyInfo>(currentPath)
                    {
                        property
                    };

                    if (property.GetCustomAttribute<LogMeAttribute>() != null)
                    {
                        results.Add(new ObjectPropertyPath(newPath));
                    }

                    var propType = property.PropertyType;

                    if (propType.IsComplexType())
                    {
                        queue.Enqueue((propType, newPath));
                    }
                }
            }

            var filtered = results
                .Where(p => !results.Any(other => IsPrefixOf(p, other)))
                .ToList();

            return filtered;
        }

        private static bool IsPrefixOf(ObjectPropertyPath shorter, ObjectPropertyPath longer)
        {
            if (shorter.Chain.Count >= longer.Chain.Count)
            {
                return false;
            }

            for (int i = 0; i < shorter.Chain.Count; i++)
            {
                if (shorter.Chain[i] != longer.Chain[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
