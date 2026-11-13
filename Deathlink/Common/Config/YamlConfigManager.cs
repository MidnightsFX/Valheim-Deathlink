using Deathlink.Common;
using System;
using System.Collections.Generic;
using System.IO;

#pragma warning disable IDE0130
namespace Deathlink.Common {
#pragma warning restore IDE0130

    // The registry every yaml config file goes through.
    //
    // Owns the parts that are identical for every file -- resolving the path, generating a default file,
    // keeping the documented header in front of the content on every write, watching for edits, wiring
    // the sync RPC -- so a config file is a declaration rather than a implementation. Add yours in
    // RegisterConfigFiles(); see Examples/ExampleYamlConfig.cs.
    internal static partial class YamlConfigManager {
        private static bool initialized;
        private static readonly List<YamlConfigFile> Files = new List<YamlConfigFile>();
        private static readonly Dictionary<string, YamlConfigFile> ByPath =
            new Dictionary<string, YamlConfigFile>(StringComparer.OrdinalIgnoreCase);

        internal static IEnumerable<YamlConfigFile> All {
            get { return Files; }
        }

        // Call once from Awake, after the mod's config class is constructed (this reads cfgFolder,
        // ConfigPollIntervalSeconds and ConfigApplyDelay from it).
        internal static void Init() {
            if (initialized) { return; }
            initialized = true;

            // Before any file registers: RegisterFile needs NetworkManager, and the sync-state teardown
            // patch has to be in place before a world is ever loaded.
            ConfigNetwork.Init();

            // On by default rather than left to the mod. An unrecognised enum VALUE is the most likely
            // mistake in a hand-edited config and, without this, the only one that still takes the whole
            // file down -- IgnoreUnmatchedProperties covers keys, not values. Add your own converters
            // through YamlFormat.AddTypeConverter before calling Init.
            YamlFormat.AddTypeConverter(new TolerantEnumConverter());

            RegisterConfigFiles();
            ConfigFileWatcher.Initialize();

            Logger.LogDebug($"Registered {Files.Count} yaml config files.");
        }

        // The only way to add a file. Registering after Init has run is supported and is how a config
        // whose defaults need game state gets in -- hook PrefabManager.OnPrefabsRegistered (or whatever
        // your defaults depend on) and call this from there.
        internal static TFile Register<TFile>(TFile file) where TFile : YamlConfigFile {
            if (file == null) { return null; }
            Files.Add(file);
            if (initialized) { Prepare(file); }
            return file;
        }

        internal static YamlConfigFile Find(string fileNameOrPath) {
            if (string.IsNullOrEmpty(fileNameOrPath)) { return null; }
            if (ByPath.TryGetValue(fileNameOrPath, out YamlConfigFile found)) { return found; }
            for (int i = 0; i < Files.Count; i++) {
                if (string.Equals(Files[i].FileName, fileNameOrPath, StringComparison.OrdinalIgnoreCase)) {
                    return Files[i];
                }
            }
            return null;
        }

        // Creates the directory as a side effect, so callers can treat the result as ready to write into.
        internal static string ConfigDirectory(string subFolder = null) {
            string path = Path.Combine(BepInEx.Paths.ConfigPath, ValConfig.cfgFolder);
            if (string.IsNullOrEmpty(subFolder) == false) { path = Path.Combine(path, subFolder); }
            return Directory.CreateDirectory(path).FullName;
        }

        internal static void ReloadFromDisk(YamlConfigFile file, bool broadcast = true) {
            if (file == null) { return; }

            try {
                if (File.Exists(file.Path) == false) {
                    Logger.LogWarning($"{file.FileName} is no longer on disk; rewriting it with this mod's built-in defaults.");
                    RestoreDefaults(file);
                }

                bool loaded = file.LoadFrom(File.ReadAllText(file.Path), ConfigOrigin.LocalFile);
                // Only a clean load goes out to the peers. Pushing a file that just failed to parse would
                // hand every client the same breakage the admin is still fixing.
                if (loaded && broadcast) { ConfigNetwork.Broadcast(file); }
            } catch (Exception e) {
                Logger.LogError($"Could not reload {file.FileName}: {e.Message}");
            }
        }

        // The single apply path for every editor-driven change, whether it came from a panel on this
        // machine or from an admin's upload. Returns false with a human-readable reason on refusal.
        //
        // Yaml text is the unit of exchange rather than a live object, deliberately. Text is the one
        // representation that is identical on disk, on the wire and after validation; an object overload
        // would need a second validation path and would hand Value an object the editor still holds a
        // reference to, which is exactly how a mod ends up serving edited-but-stale cached values.
        internal static bool ApplyEdited(YamlConfigFile file, string yaml, out string message) {
            message = "";
            if (file == null) { message = "no config file was named"; return false; }

            // Only the machine that owns the file may take this path. A client's editor goes through
            // ConfigNetwork.RequestEdit and the SERVER ends up here instead.
            if (ZNet.instance != null && ZNet.instance.IsServer() == false) {
                message = $"{file.FileName} belongs to the server; changes have to be sent to it.";
                return false;
            }

            ValidationReport report = file.DryRun(yaml, out string parseError);
            if (parseError != null) {
                message = $"{file.FileName} was rejected because {parseError}.";
                return false;
            }
            if (report.HasErrors) {
                message = string.Join(" ", report.Errors.ToArray());
                return false;
            }

            if (file.LoadFrom(yaml, ConfigOrigin.Api) == false) {
                message = file.LastError ?? $"{file.FileName} could not be applied.";
                return false;
            }

            // The exact bytes that were validated, so what is on disk is what was judged -- and the
            // documented header survives, because this goes through WriteRawToDisk.
            bool written = WriteRawToDisk(file, yaml);

            // Explicit, and required: WriteRawToDisk re-stamps the watcher so it will not see our own
            // write, which means the watcher-driven broadcast never fires for this path. Peers get the
            // applied text (LastAppliedText), not the disk, so this holds even when the write failed.
            ConfigNetwork.Broadcast(file);

            message = report.Warnings.Count == 0 ? "" : string.Join(" ", report.Warnings.ToArray());
            // Still a success: the values are live here and on every peer. What failed is keeping them past a
            // restart, which the editor has to hear about rather than a plain "saved".
            if (written == false) {
                message = ($"{file.FileName} is applied but could not be written to disk, so it reverts on restart. " + message).Trim();
            }
            return true;
        }

        // So an editor cannot pick its own serializer and hand back a document the framework would never
        // have written itself.
        internal static string SerializeForEdit<T>(YamlConfigFile<T> file, T value) where T : class {
            if (file == null || value == null) { return ""; }
            return file.EffectiveFormat.Serializer.Serialize(value);
        }

        internal static void RestoreDefaults(YamlConfigFile file) {
            WriteRawToDisk(file, file?.SerializeDefaults());
        }

        internal static void WriteCurrentToDisk(YamlConfigFile file) {
            // Value has moved on from the text it was loaded from, so the sync serializes it from now on.
            if (file != null) { file.LastAppliedText = null; }
            WriteRawToDisk(file, file?.SerializeCurrent());
        }

        // Every write to a registered config file goes through here rather than File.WriteAllText.
        //
        // The header is the only documentation of the schema an admin ever sees -- it is what tells them
        // which fields exist, what the enums accept and what the numbers mean. A bare write silently
        // deletes it and nobody notices until someone needs it.
        //
        // Returns false when nothing was written, so a caller that promises "saved" can tell.
        internal static bool WriteRawToDisk(YamlConfigFile file, string serializedYaml) {
            if (file == null || string.IsNullOrEmpty(file.Path)) { return false; }

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(file.Path));
                // Scoped so the handle is closed before RefreshStamp reads the file's length and write
                // time -- a using declaration would dispose after that and re-seed a stale stamp.
                using (StreamWriter writer = new StreamWriter(file.Path)) {
                    if (string.IsNullOrEmpty(file.Header) == false) { writer.WriteLine(file.Header); }
                    writer.WriteLine(StripLeadingHeader(serializedYaml, file.Header));
                }
                ConfigFileWatcher.RefreshStamp(file.Path);
                return true;
            } catch (Exception e) {
                Logger.LogError($"Could not write {file.FileName}: {e.Message}");
                return false;
            }
        }

        // Drops every copy of the header already at the top of a payload, so the one written above it is
        // the only one. The server sends clients its file text with the header included, and without this
        // every client copy ended up with two. Stripping every copy, not just one, also repairs a file
        // that already has several.
        //
        // A copy is a run of comment lines that opens with the header's first line and closes with its
        // last line. Only those two are compared, not the text in between, because a server keeps
        // whatever header version its file was written with until it next rewrites the file, and that
        // older copy has to be recognised too. A non-comment line ends the search, so content is never
        // touched. Line endings and trailing whitespace are ignored; everything after the header is
        // returned exactly as it came in.
        internal static string StripLeadingHeader(string yaml, string header) {
            if (string.IsNullOrEmpty(yaml) || string.IsNullOrEmpty(header)) { return yaml; }

            List<string> headerLines = new List<string>();
            foreach (string line in header.Split('\n')) {
                string trimmed = line.Trim();
                if (trimmed.Length > 0) { headerLines.Add(trimmed); }
            }
            if (headerLines.Count == 0) { return yaml; }
            string opening = headerLines[0];
            string closing = headerLines[headerLines.Count - 1];

            int start = 0;
            while (true) {
                int cursor = start;
                bool opened = false;
                bool closed = false;
                while (cursor < yaml.Length) {
                    int newline = yaml.IndexOf('\n', cursor);
                    int lineEnd = newline < 0 ? yaml.Length : newline;
                    string line = yaml.Substring(cursor, lineEnd - cursor).Trim();
                    cursor = newline < 0 ? yaml.Length : newline + 1;

                    if (line.Length == 0) { continue; }
                    if (line.StartsWith("#") == false) { break; }
                    if (opened == false) {
                        if (line != opening) { break; }
                        opened = true;
                        // A one-line header opens and closes on the same line.
                        if (headerLines.Count == 1) { closed = true; break; }
                        continue;
                    }
                    if (line == closing) { closed = true; break; }
                }
                if (closed == false) { break; }
                start = cursor;
            }
            return start == 0 ? yaml : yaml.Substring(start);
        }

        // Re-run every file's validator against the values already loaded. Call this once the game state a
        // validator depends on exists, and whenever a BepInEx entry a validator cross-checks has changed.
        // Validators waiting on the prefab table have RevalidatePrefabDependent instead.
        internal static void RevalidateAll() {
            for (int i = 0; i < Files.Count; i++) {
                try {
                    Files[i].Revalidate();
                } catch (Exception e) {
                    Logger.LogError($"Revalidating {Files[i].FileName} threw: {e.Message}");
                }
            }
        }

        // Re-run only the validators that resolve prefab names, which is what NeedsPrefabs marks. Kept
        // separate from RevalidateAll so the deferred prefab pass -- which fires off a player load -- does
        // not re-log every unrelated file's report as a side effect.
        internal static void RevalidatePrefabDependent() {
            for (int i = 0; i < Files.Count; i++) {
                if (Files[i].NeedsPrefabs == false) { continue; }
                try {
                    Files[i].Revalidate();
                } catch (Exception e) {
                    Logger.LogError($"Revalidating {Files[i].FileName} threw: {e.Message}");
                }
            }
        }

        // True when the text is null/whitespace or consists only of comments, blank lines and bare YAML
        // document markers. Such a file deserializes to null rather than throwing, so without this check
        // an admin who empties a file to "start over" gets a silent fallback to built-in defaults with no
        // idea why their edits do nothing.
        internal static bool HasNoUsableConfig(string yamlText) {
            if (string.IsNullOrWhiteSpace(yamlText)) { return true; }
            foreach (string line in yamlText.Split('\n')) {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed == "---" || trimmed == "...") { continue; }
                return false;
            }
            return true;
        }

        // Three steps, each caught on its own. One unusable file must not take Awake down with it -- and a
        // load that throws must not take the file's sync and watcher down with it either: unregistered, the
        // file never reached a joining client (who then ran on its own defaults) and fixing it on disk was
        // never noticed.
        private static void Prepare(YamlConfigFile file) {
            try {
                file.Path = Path.Combine(ConfigDirectory(file.SubFolder), file.FileName);
                ByPath[file.Path] = file;
            } catch (Exception e) {
                // Without a path there is nothing to load, sync or watch.
                Logger.LogError($"Could not prepare {file.FileName}: {e}");
                return;
            }

            try {
                if (File.Exists(file.Path) == false) {
                    Logger.LogDebug($"{file.FileName} missing, writing this mod's built-in defaults.");
                    RestoreDefaults(file);
                } else if (HasNoUsableConfig(File.ReadAllText(file.Path))) {
                    Logger.LogWarning($"{file.FileName} was empty and has been overwritten with this mod's " +
                        $"built-in defaults. File: {file.Path}");
                    RestoreDefaults(file);
                }

                file.LoadFrom(File.Exists(file.Path) ? File.ReadAllText(file.Path) : "", ConfigOrigin.Startup);
            } catch (Exception e) {
                Logger.LogError($"Could not load {file.FileName}, using this mod's built-in defaults: {e}");
            }
            // A load that threw skipped the failure policy, so there may be no value at all yet.
            try {
                file.UseDefaultsIfUnloaded();
            } catch (Exception e) {
                Logger.LogError($"Could not apply the built-in defaults for {file.FileName}: {e}");
            }

            try {
                ConfigNetwork.RegisterFile(file);
                if (file.Watch) { ConfigFileWatcher.Register(file.Path, OnWatchedFileChanged); }
            } catch (Exception e) {
                Logger.LogError($"Could not register {file.FileName} for sync and file watching: {e}");
            }
        }

        private static void OnWatchedFileChanged(string path) {
            if (ByPath.TryGetValue(path, out YamlConfigFile file) == false) { return; }
            // A connected client runs the server's copy of a server-authoritative file; its own file only
            // comes back into play once it leaves.
            if (file.Sync == ConfigSyncMode.ServerAuthoritative && ZNet.instance != null && ZNet.instance.IsServer() == false) { return; }

            // Debounced rather than reloaded straight off the poll: most editors save by truncating and
            // then writing, which the watcher sees as two separate changes, and a reload is not cheap
            // (parse, validate, apply, broadcast to every peer).
            ConfigChangeDebouncer.Schedule(file, () => ReloadFromDisk(file, broadcast: true));
        }
    }
}
