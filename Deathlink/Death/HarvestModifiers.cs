using Deathlink.Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    /// <summary>
    /// Scales harvested drops by the death choice's resource multiplier. Trees, mine rocks and
    /// destructibles build their drops through <see cref="DropTable.GetDropList"/>, so that list is
    /// rescaled; pickables drop one unit at a time, so each drop's stack is rescaled.
    /// </summary>
    public static class HarvestModifiers
    {
        // Containers and creatures use DropTable too, so the GetDropList postfix only scales while a
        // harvestable has this set. Everything here runs on the main thread, so a static flag is enough.
        internal static bool ScalingActive;

        private static bool HasResourceModifiers()
        {
            return Deathlink.pcfg().HasResourceModifiers(ResourceGainTypes.Harvesting);
        }

        private static bool HasLootModifiers()
        {
            return Deathlink.pcfg().HasLoot(ResourceGainTypes.Harvesting);
        }

        // Only the harvester's own machine applies the modifier and bonus loot. The two are independent:
        // a level with only harvest bonus loot must still open the bracket so the loot rolls.
        private static bool ShouldHandleHarvest(HitData hit)
        {
            if (Player.m_localPlayer == null) { return false; }
            if (!HasResourceModifiers() && !HasLootModifiers()) { return false; }
            if (hit == null) { return false; }
            return hit.m_attacker == Player.m_localPlayer.GetZDOID();
        }

        // True when this call opened the bracket and so must close it.
        private static bool BeginScaling(HitData hit)
        {
            if (ScalingActive || !ShouldHandleHarvest(hit)) { return false; }
            ScalingActive = true;
            return true;
        }

        // Keeps the expected value: the fractional part is the chance of one more. Can return 0.
        public static int ScaleCount(int baseCount, float m)
        {
            if (baseCount <= 0) { return 0; }
            float scaled = baseCount * m;
            int whole = Mathf.FloorToInt(scaled);
            float frac = scaled - whole;
            if (frac > 0f && UnityEngine.Random.value < frac) { whole += 1; }
            return whole;
        }

        // GetDropList returns one entry per dropped unit.
        public static void ScaleDropListInPlace(List<GameObject> drops)
        {
            if (drops == null || drops.Count == 0) { return; }

            Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();
            List<GameObject> order = new List<GameObject>();
            foreach (GameObject go in drops)
            {
                if (go == null) { continue; }
                if (counts.TryGetValue(go, out int c)) { counts[go] = c + 1; }
                else { counts[go] = 1; order.Add(go); }
            }

            drops.Clear();
            foreach (GameObject go in order)
            {
                float m = Deathlink.pcfg().GetResourceModifier(go, ResourceGainTypes.Harvesting);
                int finalCount = (m == 1f) ? counts[go] : ScaleCount(counts[go], m);
                if (finalCount != counts[go])
                {
                    Logger.LogDebug($"Scaling harvest drop {go.name}: {counts[go]} -> {finalCount} (x{m})");
                }
                for (int i = 0; i < finalCount; i++) { drops.Add(go); }
            }
        }

        private static void SpawnHarvestBonusLoot(Vector3 position)
        {
            List<KeyValuePair<GameObject, int>> harvestloot = Deathlink.pcfg().RollLoot(ResourceGainTypes.Harvesting);
            if (harvestloot.Count == 0) { return; }
            foreach (var kvp in harvestloot)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                    ItemDrop.OnCreateNew(UnityEngine.Object.Instantiate(kvp.Key, position, rotation));
                }
            }
        }

        // GetDropList() returns a freshly allocated list, so mutating __result in place is safe.
        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        public static class ScaleHarvestDropList
        {
            private static void Postfix(List<GameObject> __result)
            {
                if (!ScalingActive || !HasResourceModifiers()) { return; }
                ScaleDropListInPlace(__result);
            }
        }

        // Each bracket scales only its own call's GetDropList, then rolls the bonus loot. __state says
        // whether this call owns the bracket, so a nested destroy does not clear an outer one's flag.
        // A Finalizer runs even when the original throws, so the flag never sticks on.
        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
        public static class TreeLogHarvestScale
        {
            private static void Prefix(HitData hitData, out bool __state)
            {
                __state = BeginScaling(hitData);
            }

            private static void Finalizer(TreeLog __instance, bool __state)
            {
                if (!__state) { return; }
                ScalingActive = false;
                SpawnHarvestBonusLoot(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
        public static class DestructibleHarvestScale
        {
            private static void Prefix(Destructible __instance, HitData hit, out bool __state)
            {
                __state = false;
                // Rocks that spawn a fracture (a MineRock5) drop via that fracture, not here; let the
                // MineRock5 bracket handle those so the modifier isn't applied twice.
                if (__instance.m_spawnWhenDestroyed != null) { return; }
                // A destructible that drops nothing is not a harvest, so it rolls no bonus loot.
                DropOnDestroyed drops = __instance.GetComponent<DropOnDestroyed>();
                if (drops == null || drops.m_dropWhenDestroyed == null || drops.m_dropWhenDestroyed.IsEmpty()) { return; }
                __state = BeginScaling(hit);
            }

            private static void Finalizer(Destructible __instance, bool __state)
            {
                if (!__state) { return; }
                ScalingActive = false;
                SpawnHarvestBonusLoot(__instance.transform.position);
            }
        }

        // The real vanilla mine drop happens in DamageArea (owner-only), not in RPC_SetAreaHealth
        // (which runs on every client). DamageArea is private, so it is patched by name.
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static class MineRockHarvestScale
        {
            private static void Prefix(HitData hit, out bool __state)
            {
                __state = BeginScaling(hit);
            }

            // __result is DamageArea's return value: true only when the hit area was destroyed and
            // therefore actually dropped, so bonus loot only rolls on a genuine harvest.
            private static void Finalizer(MineRock5 __instance, bool __state, bool __result)
            {
                if (!__state) { return; }
                ScalingActive = false;
                if (__result) { SpawnHarvestBonusLoot(__instance.transform.position); }
            }
        }

        // Single-piece deposits (tin, obsidian, meteorite and the like) are MineRock, not MineRock5, and drop
        // from RPC_Hit. __state is the hit area's health before the hit, or -1 when this call does not own
        // the bracket.
        [HarmonyPatch(typeof(MineRock), "RPC_Hit")]
        public static class SingleMineRockHarvestScale
        {
            private static void Prefix(MineRock __instance, HitData hit, int hitAreaIndex, out float __state)
            {
                __state = -1f;
                ZDO zdo = __instance.m_nview != null ? __instance.m_nview.GetZDO() : null;
                if (zdo == null || ScalingActive || !ShouldHandleHarvest(hit)) { return; }
                float health = zdo.GetFloat("Health" + hitAreaIndex, __instance.GetHealth());
                if (health <= 0f) { return; }
                ScalingActive = true;
                __state = health;
            }

            private static void Finalizer(MineRock __instance, int hitAreaIndex, float __state)
            {
                if (__state <= 0f) { return; }
                ScalingActive = false;
                ZDO zdo = __instance.m_nview != null ? __instance.m_nview.GetZDO() : null;
                if (zdo != null && zdo.GetFloat("Health" + hitAreaIndex, __state) <= 0f) {
                    SpawnHarvestBonusLoot(__instance.transform.position);
                }
            }
        }

        // Pickable.Drop is called once per drop: the main item with stack 1, each extra drop with its
        // own stack.
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Drop))]
        public static class ScalePickableDrop
        {
            private static bool Prefix(GameObject prefab, ref int stack)
            {
                if (Player.m_localPlayer == null || !HasResourceModifiers() || prefab == null) { return true; }
                float m = Deathlink.pcfg().GetResourceModifier(prefab, ResourceGainTypes.Harvesting);
                if (m == 1f) { return true; }

                int count = ScaleCount(stack, m);
                if (count <= 0) { return false; }
                stack = count;
                return true;
            }
        }
    }
}
