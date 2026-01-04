using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;

namespace WOTRMultiplayer.UnitTests.HarmonyPatches
{
    [TestFixture]
    public class HarmonyPatchesTests
    {
        // duplicate due to changes to ID generation + starting Mercenary leveling mode. No reason to merge as of now
        private readonly HashSet<string> _justifiedTargets = new(["Kingmaker.Player.CreateCustomCompanion..[].HarmonyTranspiler"]);
        private readonly HashSet<string> _justifiedNames = new(["Player_CreateCustomCompanion_Transpiler"]);

        [Test]
        public void HarmonyPatch_NoDuplicateTargets()
        {
            // Arrange
            var counter = new ConcurrentDictionary<string, int>();

            // Act
            foreach (var enumerated in EnumeratePatches())
            {
                var target = enumerated.Method.GetCustomAttribute<HarmonyPatch>();

                var fullTargetName = $"{target.info.declaringType.FullName}.{target.info.methodName}.{target.info.methodType}.[{string.Join(",", (target.info.argumentTypes ?? []).Select(x => x.Name))}].{enumerated.PatchType}";
                counter.AddOrUpdate(fullTargetName, 1, (key, value) => value + 1);
            }
            var duplicated = counter.Where(x => !_justifiedTargets.Contains(x.Key) && x.Value > 1).ToList();

            // Assert
            Assert.That(duplicated, Is.Empty, "Duplicate harmony patch targets detected");
        }

        [Test]
        public void HarmonyPatch_NoDuplicateMethodNames()
        {
            // Arrange
            var counter = new ConcurrentDictionary<string, int>();

            // Act
            foreach (var enumerated in EnumeratePatches())
            {
                counter.AddOrUpdate(enumerated.Method.Name, 1, (key, value) => value + 1);
            }
            var duplicated = counter.Where(x => !_justifiedNames.Contains(x.Key) && x.Value > 1).ToList();

            // Assert
            Assert.That(duplicated, Is.Empty, "Duplicate harmony patch method names detected");
        }

        private IEnumerable<EnumeratedHarmonyPatch> EnumeratePatches()
        {
            var classesWithPatches = typeof(Main).Assembly.GetTypes().Where(x => x.GetCustomAttribute<HarmonyPatch>() != null);
            foreach (var classPatch in classesWithPatches)
            {
                var methods = classPatch.GetMethods().Where(x => x.GetCustomAttribute(typeof(HarmonyPatch)) != null);
                foreach (var method in methods)
                {
                    var patchType = method.GetCustomAttribute<HarmonyPrefix>()?.GetType().Name
                        ?? method.GetCustomAttribute<HarmonyPostfix>()?.GetType().Name
                        ?? method.GetCustomAttribute<HarmonyTranspiler>()?.GetType().Name;

                    yield return new EnumeratedHarmonyPatch { Class = classPatch, Method = method, PatchType = patchType };
                }
            }
        }

        private class EnumeratedHarmonyPatch
        {
            public Type Class { get; set; }

            public MethodInfo Method { get; set; }

            public string PatchType { get; set; }
        }
    }
}
