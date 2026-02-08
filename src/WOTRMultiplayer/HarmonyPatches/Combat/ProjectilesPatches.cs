using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Controllers.Projectiles;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class ProjectilesPatches
    {
        [HarmonyPatch(typeof(Projectile), nameof(Projectile.CalculateMissTarget))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Projectile_CalculateMissTarget_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var target = PatchesUtils.GetTranspilerTarget(MethodBase.GetCurrentMethod());
            var lookFor = AccessTools.PropertyGetter(typeof(UnityEngine.Random), nameof(UnityEngine.Random.insideUnitCircle));
            var replaceWith = AccessTools.Method(typeof(ProjectilesPatches), nameof(ProjectilesPatches.CalculateFallOnMiss));
            var matcher = new CodeMatcher(instructions);
            var match = matcher.SearchForward(x => x.Calls(lookFor));
            if (match.IsInvalid)
            {
                Main.GetLogger<ProjectilesPatches>().LogError("Transpiler has not been applied. Target={Target}", target);
                return instructions;
            }

            var labels = match.Instruction.ExtractLabels();
            var newInstructions = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
                new (OpCodes.Ldloc_2),
                new (OpCodes.Call, replaceWith),
            };
            match = match.RemoveInstructions(18).Insert(newInstructions);

            Main.GetLogger<ProjectilesPatches>().LogInformation("Transpiler has been applied. Target={Target}", target);
            return matcher.Instructions();
        }

        private static Vector2 CalculateFallOnMiss(Projectile projectile, float cameraOffset)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(Mathf.Min(projectile.Blueprint.MissMinRadius + cameraOffset, projectile.Blueprint.MissMaxRadius), projectile.Blueprint.MissMaxRadius);
            }

            var attackRoll = projectile.m_AttackRoll;
            var attackWithWeapon = attackRoll.RuleAttackWithWeapon;
            var initiatorId = attackRoll.Initiator.UniqueId;
            var targetId = attackRoll.Target.UniqueId;
            var weaponName = attackRoll.Weapon?.NameForAcronym;
            var weaponId = attackRoll.Weapon?.Blueprint.AssetGuid;
            var sessionSeed = Main.Multiplayer.GetSessionSeed();
            var combatSeed = Main.Multiplayer.GetCombatSeed();
            var identifier = $"{nameof(Projectile)}:{nameof(CalculateFallOnMiss)}:{attackRoll.Initiator.UniqueId}:{targetId}:{weaponName}:{weaponId}:{attackRoll.IsCriticalRoll}:{attackRoll.AttackType}:{attackWithWeapon?.AttackNumber}:{attackWithWeapon?.AttacksCount}:{sessionSeed}:{combatSeed}";

            var pointInCircle = Main.Multiplayer.ValueGenerator.GetRandomUnitCircle(SeedLifetime.Combat, identifier);

            var minOffset = Mathf.Min(projectile.Blueprint.MissMinRadius + cameraOffset, projectile.Blueprint.MissMaxRadius);
            var maxOffset = projectile.Blueprint.MissMaxRadius;
            var offset = Main.Multiplayer.ValueGenerator.Range(SeedLifetime.Combat, identifier, minOffset, maxOffset);

            var resultedPoint = pointInCircle.ToUnityVector2().normalized * offset;
            Main.GetLogger<ProjectilesPatches>().LogInformation("Projectile fall on miss has been calculated. InitiatorId={InitiatorId}, TargetId={TargetId}, WeaponName={WeaponName}, WeaponId={WeaponId}, Point={Point}", initiatorId, targetId, weaponName, weaponId, resultedPoint);
            return resultedPoint;
        }
    }
}
