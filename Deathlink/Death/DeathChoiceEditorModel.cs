using Deathlink.Common;
using System;
using System.Collections.Generic;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    // The staged, editable copy of DeathChoices.yaml that the editor panel works on.
    //
    // Kept free of Unity types on purpose: the key operations below are the part worth being able to read
    // and reason about, and burying them in panel-building code is how StarLevelSystem's equivalent grew
    // to 1250 lines.
    internal sealed class DeathChoiceEditorModel
    {
        internal Dictionary<string, DeathChoiceLevel> Levels = new Dictionary<string, DeathChoiceLevel>();

        // Explicit key order. A rename is remove-then-add, and a plain Dictionary would drop the level at
        // the end of the file every time -- which quietly reshuffles an admin's carefully ordered config.
        internal List<string> Order = new List<string>();

        internal string SelectedKey;

        // What DeathChoices.LastLoadedUtc said when this snapshot was taken, so Apply can notice that the
        // server's copy moved underneath a long edit.
        internal DateTime BaseLoadedUtc;

        private static YamlFormat Format {
            get {
                YamlConfigFile<Dictionary<string, DeathChoiceLevel>> file = YamlConfigManager.DeathChoices;
                return file != null ? file.EffectiveFormat : YamlFormat.Default;
            }
        }

        // Deep-copies the live levels through a serialize/deserialize round trip.
        //
        // This is the ONLY way the editor may obtain its working set, and the round trip is doing three
        // jobs at once: the editor never touches an object the rest of the mod is reading; the private
        // lazy caches on DeathChoiceLevel (CalculatedResourceMods, KillLootModifiers, CalculatedSkillMods
        // and their *Cached flags) are populated on first use and NEVER invalidated, so a level mutated in
        // place would keep serving pre-edit values for the rest of the session; and the copy is faithful,
        // because every non-default initializer in DataObjects carries [DefaultValue].
        internal static DeathChoiceEditorModel Snapshot() {
            DeathChoiceEditorModel model = new DeathChoiceEditorModel();
            YamlConfigFile<Dictionary<string, DeathChoiceLevel>> file = YamlConfigManager.DeathChoices;

            Dictionary<string, DeathChoiceLevel> copy = null;
            if (file != null && file.Value != null) {
                try {
                    YamlFormat format = file.EffectiveFormat;
                    copy = format.Deserializer.Deserialize<Dictionary<string, DeathChoiceLevel>>(
                        format.Serializer.Serialize(file.Value));
                } catch (Exception e) {
                    Logger.LogError($"Could not copy the death levels for editing: {e.Message}");
                }
            }

            model.Levels = copy ?? new Dictionary<string, DeathChoiceLevel>();
            model.Order = new List<string>(model.Levels.Keys);
            model.SelectedKey = model.Order.Count > 0 ? model.Order[0] : null;
            model.BaseLoadedUtc = file != null ? file.LastLoadedUtc : DateTime.MinValue;
            return model;
        }

        // message carries the reason on failure, and occasionally a note on success -- deleting the
        // fallback level moves it and says where.
        internal bool TryAdd(string key, out string message) {
            key = (key ?? "").Trim();
            if (IsUsableKey(key, out message) == false) { return false; }
            if (Levels.ContainsKey(key)) {
                message = $"A level called '{key}' already exists.";
                return false;
            }

            Levels[key] = new DeathChoiceLevel() { DisplayName = key, DeathStyle = new DeathProgressionDetails() };
            Order.Add(key);
            SelectedKey = key;
            message = NearMatchWarning(key);
            return true;
        }

        internal bool TryRename(string oldKey, string newKey, out string message) {
            message = null;
            newKey = (newKey ?? "").Trim();
            if (Levels.ContainsKey(oldKey) == false) {
                message = "That level no longer exists.";
                return false;
            }
            if (IsUsableKey(newKey, out message) == false) { return false; }
            if (string.Equals(oldKey, newKey, StringComparison.Ordinal)) { return true; }
            if (Levels.ContainsKey(newKey)) {
                message = $"A level called '{newKey}' already exists.";
                return false;
            }

            DeathChoiceLevel level = Levels[oldKey];
            Levels.Remove(oldKey);
            Levels[newKey] = level;

            int index = Order.IndexOf(oldKey);
            if (index >= 0) { Order[index] = newKey; } else { Order.Add(newKey); }
            if (string.Equals(SelectedKey, oldKey, StringComparison.Ordinal)) { SelectedKey = newKey; }

            message = NearMatchWarning(newKey);
            return true;
        }

        internal bool TryDelete(string key, out string message) {
            message = null;
            if (Levels.ContainsKey(key) == false) {
                message = "That level no longer exists.";
                return false;
            }
            // Caught here rather than left to the validator, so the button refuses instead of the Apply.
            if (Levels.Count <= 1) {
                message = "There has to be at least one death level.";
                return false;
            }

            bool wasFallback = Levels[key].Fallback;
            Levels.Remove(key);
            Order.Remove(key);
            if (string.Equals(SelectedKey, key, StringComparison.Ordinal)) {
                SelectedKey = Order.Count > 0 ? Order[0] : null;
            }

            if (wasFallback && Order.Count > 0) {
                Levels[Order[0]].Fallback = true;
                message = $"'{key}' was the fallback level, so '{Order[0]}' is now.";
            }
            return true;
        }

        internal string Duplicate(string key) {
            if (Levels.ContainsKey(key) == false) { return null; }

            DeathChoiceLevel copy;
            try {
                YamlFormat format = Format;
                copy = format.Deserializer.Deserialize<DeathChoiceLevel>(format.Serializer.Serialize(Levels[key]));
            } catch (Exception e) {
                Logger.LogError($"Could not duplicate '{key}': {e.Message}");
                return null;
            }
            if (copy == null) { return null; }

            // Only one level may be the fallback, and the original already is if anything is.
            copy.Fallback = false;

            string newKey = UniqueKey(key + "Copy");
            Levels[newKey] = copy;
            int index = Order.IndexOf(key);
            if (index >= 0) { Order.Insert(index + 1, newKey); } else { Order.Add(newKey); }
            SelectedKey = newKey;
            return newKey;
        }

        // Radio semantics: exactly one level carries the flag.
        internal void SetFallback(string key) {
            foreach (KeyValuePair<string, DeathChoiceLevel> pair in Levels) {
                if (pair.Value == null) { continue; }
                pair.Value.Fallback = string.Equals(pair.Key, key, StringComparison.Ordinal);
            }
        }

        internal string Serialize() {
            // Rebuilt in Order so the file keeps the shape the admin gave it.
            Dictionary<string, DeathChoiceLevel> ordered = new Dictionary<string, DeathChoiceLevel>();
            foreach (string key in Order) {
                if (Levels.TryGetValue(key, out DeathChoiceLevel level)) { ordered[key] = level; }
            }
            // Anything that somehow escaped Order still gets written -- losing a level to a bookkeeping
            // slip would be far worse than an out-of-place one.
            foreach (KeyValuePair<string, DeathChoiceLevel> pair in Levels) {
                if (ordered.ContainsKey(pair.Key) == false) { ordered[pair.Key] = pair.Value; }
            }
            return YamlConfigManager.SerializeForEdit(YamlConfigManager.DeathChoices, ordered);
        }

        // Validated against the LIVE levels as "previous", which is what makes the removed-level warning
        // fire in the panel before the admin commits to it.
        internal ValidationReport Validate() {
            return DeathConfigurationData.ValidateDeathLevels(Levels, DeathConfigurationData.DeathLevels);
        }

        private static bool IsUsableKey(string key, out string message) {
            message = null;
            if (string.IsNullOrWhiteSpace(key)) {
                message = "A level needs a name.";
                return false;
            }
            // Not a full yaml-key validation, just the three that would produce a file the admin then has
            // to repair by hand.
            if (key.IndexOf(':') >= 0 || key.IndexOf('#') >= 0 || key.StartsWith("-")) {
                message = "A level name cannot contain ':' or '#', or start with '-'.";
                return false;
            }
            return true;
        }

        // Keys are compared case-sensitively everywhere they matter, but nobody expects that of a name
        // they typed, so a near-miss is worth saying out loud without refusing it.
        private string NearMatchWarning(string key) {
            foreach (string existing in Order) {
                if (string.Equals(existing, key, StringComparison.Ordinal)) { continue; }
                if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)) {
                    return $"'{key}' differs from '{existing}' only by capitalisation. They are two separate levels.";
                }
            }
            return null;
        }

        private string UniqueKey(string candidate) {
            if (Levels.ContainsKey(candidate) == false) { return candidate; }
            for (int i = 2; i < 1000; i++) {
                string attempt = candidate + i;
                if (Levels.ContainsKey(attempt) == false) { return attempt; }
            }
            return candidate + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
