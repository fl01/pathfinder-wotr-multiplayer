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

                var fullTargetName = $"{enumerated.PatchedClass.FullName}.{target.info.methodName}.{target.info.methodType}.[{string.Join(",", (target.info.argumentTypes ?? []).Select(x => x.Name))}].{enumerated.PatchType}";
                counter.AddOrUpdate(fullTargetName, 1, (key, value) => value + 1);
            }
            var duplicated = counter.Where(x => !_justifiedTargets.Contains(x.Key) && x.Value > 1).ToList();

            // Assert
            Assert.That(duplicated, Is.Empty, "Duplicate harmony patch targets detected");
        }

        [Test]
        public void HarmonyPatch_CorrectInstanceTypeInjected()
        {
            // Arrange
            var invalidMethods = new List<string>();
            const string parameterName = "__instance";

            // Act
            foreach (var enumerated in EnumeratePatches())
            {
                var target = enumerated.Method.GetCustomAttribute<HarmonyPatch>();

                var instanceParameter = enumerated.Method.GetParameters().FirstOrDefault(x => x.Name == parameterName);
                if (instanceParameter != null && instanceParameter.ParameterType != enumerated.PatchedClass)
                {
                    invalidMethods.Add(enumerated.Method.Name);
                }
            }

            // Assert
            Assert.That(invalidMethods, Is.Empty, $"{invalidMethods.Count} invalid {parameterName} parameter(s) detected");
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

                    var patchedClass = method.GetCustomAttribute<HarmonyPatch>().info.declaringType;

                    yield return new EnumeratedHarmonyPatch { PatchedClass = patchedClass, Method = method, PatchType = patchType };
                }
            }
        }

        private class EnumeratedHarmonyPatch
        {
            public Type PatchedClass { get; set; }

            public MethodInfo Method { get; set; }

            public string PatchType { get; set; }
        }
    }
}
