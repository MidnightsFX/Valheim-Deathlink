using HarmonyLib;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death;

public static class DamageModifiers
{
    // RPC_Damage runs on the target's owner, and the multipliers are read from each player's own ZDO,
    // so the right ones apply whoever owns the target. A prefix, so they apply before armour and block.
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
    public static class DamageModifierPatch
    {
        // Runs on every hit, so the ZDO key hashes are worked out once.
        private static readonly int DamageTakenHash = DamageTakenModifierKey.GetStableHashCode();
        private static readonly int DamageDoneHash = DamageDoneModifierKey.GetStableHashCode();

        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null) { return; }

            if (__instance is Player) {
                float takenMod = GetStoredModifier(__instance, DamageTakenHash);
                if (takenMod != 1f) { hit.ApplyModifier(takenMod); }
            }

            if (hit.GetAttacker() is Player attacker) {
                float doneMod = GetStoredModifier(attacker, DamageDoneHash);
                if (doneMod != 1f) { hit.ApplyModifier(doneMod); }
            }
        }

        private static float GetStoredModifier(Character character, int keyHash)
        {
            if (character == null) { return 1f; }
            ZNetView nview = character.m_nview;
            if (nview == null || !nview.IsValid()) { return 1f; }
            ZDO zdo = nview.GetZDO();
            if (zdo == null) { return 1f; }
            return zdo.GetFloat(keyHash, 1f);
        }
    }
}
