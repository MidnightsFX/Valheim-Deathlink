using HarmonyLib;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death;

public static class DamageModifiers
{
    // Scales incoming/outgoing combat damage based on each player's selected Deathlink level.
    //
    // The multipliers live on the player's character ZDO (written whenever their death choice is
    // resolved - see DeathConfigurationData.StoreDamageModifiersOnPlayer). RPC_Damage runs on the
    // machine that owns the target, so reading the values straight off the involved players' ZDOs
    // means the correct multiplier is applied no matter who owns the target - full multiplayer
    // stability, including dedicated servers and PvP.
    //
    // Runs as a Prefix so the multiplier hits the raw damage before the game applies armor/block
    // mitigation. Trees/rocks/structures are not Characters, so this only ever touches combat
    // damage and never interferes with the harvest modifiers.
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
    public static class DamageModifierPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null) { return; }

            // Damage taken: the target is a player, apply their stored damage-taken modifier.
            if (__instance is Player) {
                float takenMod = GetStoredModifier(__instance, DamageTakenModifierKey);
                if (takenMod != 1f) { hit.ApplyModifier(takenMod); }
            }

            // Damage done: the attacker is a player, apply their stored damage-done modifier. In
            // PvP both branches can run for the same hit and the multipliers correctly stack.
            if (hit.GetAttacker() is Player attacker) {
                float doneMod = GetStoredModifier(attacker, DamageDoneModifierKey);
                if (doneMod != 1f) { hit.ApplyModifier(doneMod); }
            }
        }

        // Reads a damage multiplier from a character's networked ZDO, defaulting to 1f (no change)
        // when the character has no stored value yet - e.g. before their death choice has synced.
        private static float GetStoredModifier(Character character, string key)
        {
            if (character == null) { return 1f; }
            ZNetView nview = character.m_nview;
            if (nview == null || !nview.IsValid()) { return 1f; }
            ZDO zdo = nview.GetZDO();
            if (zdo == null) { return 1f; }
            return zdo.GetFloat(key, 1f);
        }
    }
}
