using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches
{
    public class PatchesUtils
    {
        public static bool IsHelperUnit(string unitId)
        {
            return !string.IsNullOrEmpty(unitId) && unitId.StartsWith("description-helper-", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetTranspilerTarget(MethodBase method)
        {
            var attr = method.GetCustomAttribute<HarmonyPatch>();
            var target = $"{attr.info.declaringType.Name}.{attr.info.methodName ?? attr.info.methodType?.ToString()} ({method.Name})";
            return target;
        }

        public static void Dump(List<CodeInstruction> codeInstructions)
        {
            Main.GetLogger<PatchesUtils>().LogWarning(Environment.NewLine + string.Join(Environment.NewLine, codeInstructions));
        }
    }
}
