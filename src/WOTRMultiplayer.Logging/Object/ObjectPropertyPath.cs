using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WOTRMultiplayer.Logging.Object
{
    public class ObjectPropertyPath
    {
        public List<PropertyInfo> Chain { get; private set; } = [];

        public ObjectPropertyPath(List<PropertyInfo> chain)
        {
            Chain = [.. chain];
        }

        public object GetValue(object instance)
        {
            var current = instance;

            foreach (var property in Chain)
            {
                if (current == null)
                {
                    return null;
                }

                current = property.GetValue(current);
            }

            return current;
        }

        public override string ToString()
        {
            return string.Join("", Chain.Select(p => p.Name));
        }
    }
}
