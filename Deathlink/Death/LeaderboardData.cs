using Deathlink.Common;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    internal static class LeaderboardData
    {
        public static Dictionary<long, LeaderboardEntry> leaderboard = new Dictionary<long, LeaderboardEntry>();

        private static CustomRPC leaderboardRPC;

        // Added to the unique key at snapshot time rather than written on every hit.
        private static long sessionDamage = 0;

        private static bool initialReportSent = false;

        // Player.m_timeSinceDeath starts at 999999 on a character that has never died, and at that size a
        // float cannot take a frame's worth of time, so it never moves. Such a life is timed here instead:
        // the seconds before this session are kept on the character, the rest counted from its spawn.
        private const float NeverDiedTime = 999999f;
        private const string NeverDiedLifeKey = "DL_LBAlive";
        private static float lifeClockStart = -1f;
        // One entry is a few hundred characters; anything far past that is not a real snapshot.
        private const int MaxSnapshotChars = 8192;
        private static bool unsaved = false;
        // Jotunn's CustomRPC.MaximumSendQueueSize, which is private.
        private const int JotunnSendQueueLimit = 20000;

        internal static void Init()
        {
            try {
                if (File.Exists(ValConfig.leaderboardPath)) {
                    UpdateLeaderboardFromYaml(File.ReadAllText(ValConfig.leaderboardPath));
                }
            } catch (Exception e) {
                Logger.LogWarning($"Failed to load leaderboard data, starting empty. Exception: {e}");
            }
        }

        private static void Write()
        {
            try {
                File.WriteAllText(ValConfig.leaderboardPath, yamlserializer.Serialize(leaderboard));
            } catch (Exception e) {
                Logger.LogWarning($"Failed to write leaderboard data. Exception: {e}");
            }
        }

        private static void UpdateLeaderboardFromYaml(string rawyaml)
        {
            if (string.IsNullOrWhiteSpace(rawyaml)) { return; }
            var parsed = leaderboardDeserializer.Deserialize<Dictionary<long, LeaderboardEntry>>(rawyaml);
            if (parsed != null) { leaderboard = parsed; }
        }

        private static void MergeSnapshot(LeaderboardEntry entry)
        {
            if (entry == null || entry.PlayerID == 0) { return; }
            leaderboard[entry.PlayerID] = entry;
            unsaved = true;
        }

        // Saved on the sync tick rather than on every snapshot, which arrive from every client each interval.
        private static void SaveIfChanged()
        {
            if (!unsaved) { return; }
            unsaved = false;
            Write();
        }

        public static void SetupRPC()
        {
            leaderboardRPC = NetworkManager.Instance.AddRPC("DEATHLK_LB", OnServerReceiveSnapshot, OnClientReceiveBoard);
            SynchronizationManager.Instance.AddInitialSynchronization(leaderboardRPC, BuildFullBoardPackage);
        }

        private static IEnumerator OnServerReceiveSnapshot(long sender, ZPackage package)
        {
            try {
                bool wantsBoard = package.ReadBool();
                string yaml = package.ReadString();
                if (yaml.Length > MaxSnapshotChars) {
                    Logger.LogWarning($"Ignoring an oversized leaderboard snapshot ({yaml.Length} characters) from {sender}.");
                    yield break;
                }
                var entry = leaderboardDeserializer.Deserialize<LeaderboardEntry>(yaml);
                MergeSnapshot(entry);
                // Only a client opening the dashboard asks for the board, and only that client gets it
                // back; everyone else gets it from the SyncLoop broadcast.
                if (wantsBoard && leaderboardRPC != null) {
                    leaderboardRPC.SendPackage(sender, BuildFullBoardPackage());
                }
            } catch (Exception e) {
                Logger.LogWarning($"Failed to parse leaderboard snapshot from {sender}. Exception: {e}");
            }
            yield return null;
        }

        private static IEnumerator OnClientReceiveBoard(long sender, ZPackage package)
        {
            string yaml = package.ReadString();
            try {
                UpdateLeaderboardFromYaml(yaml);
                LeaderboardUI.RefreshIfVisible();
            } catch (Exception e) {
                Logger.LogWarning($"Failed to parse leaderboard board. Exception: {e}");
            }
            yield return null;
        }

        private static ZPackage BuildFullBoardPackage()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(yamlserializer.Serialize(leaderboard));
            return pkg;
        }

        private static ZPackage BuildSnapshotPackage(LeaderboardEntry snap, bool requestBoard)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(requestBoard);
            pkg.Write(yamlserializer.Serialize(snap));
            return pkg;
        }

        private static void BroadcastFullBoard()
        {
            if (ZNet.instance == null || leaderboardRPC == null) { return; }
            leaderboardRPC.SendPackage(ZNet.instance.m_peers, BuildFullBoardPackage());
        }

        private static void SendLocalSnapshotToServer(bool requestBoard = false)
        {
            LeaderboardEntry snap = BuildLocalSnapshot();
            if (snap == null) { return; }
            if (ZNet.instance != null && ZNet.instance.IsServer()) {
                MergeSnapshot(snap);
                // The host reads the authoritative board directly, so a pull is just a local refresh.
                if (requestBoard) { LeaderboardUI.RefreshIfVisible(); }
                return;
            }
            if (leaderboardRPC == null || ZRoutedRpc.instance == null) { return; }
            leaderboardRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), BuildSnapshotPackage(snap, requestBoard));
        }

        public static void RequestDashboardSync()
        {
            if (!ValConfig.EnableLeaderboard.Value || ZNet.instance == null) { return; }
            try {
                SendLocalSnapshotToServer(requestBoard: true);
            } catch (Exception e) {
                Logger.LogWarning($"Leaderboard dashboard sync failed: {e}");
            }
        }

        // Jotunn's SendPackage is a coroutine on ZNet.instance that gets no further frame during shutdown,
        // so the send routine is driven synchronously here while the server socket is still open.
        private static void FlushLocalSnapshotToServer()
        {
            if (ZNet.instance == null) { return; }

            if (ZNet.instance.IsServer()) {
                LeaderboardEntry hostSnap = BuildLocalSnapshot();
                if (hostSnap != null) { MergeSnapshot(hostSnap); }
                return;
            }

            if (Player.m_localPlayer == null || leaderboardRPC == null) { return; }
            ZNetPeer serverPeer = ZNet.instance.GetServerPeer();
            if (serverPeer == null || serverPeer.m_socket == null) { return; }
            // Jotunn waits for a full send queue to drain with a Time.time timeout, and Time.time does not
            // move inside this synchronous loop, so a backed-up connection would hang the game on logout.
            if (serverPeer.m_socket.GetSendQueueSize() > JotunnSendQueueLimit) {
                Logger.LogDebug("Send queue to the server is full, skipping the final leaderboard report.");
                return;
            }

            LeaderboardEntry snap = BuildLocalSnapshot();
            if (snap == null) { return; }

            // A small, uncompressed package finishes the routine, including its InvokeRoutedRPC, in one pass.
            IEnumerator routine = leaderboardRPC.SendPackageRoutine(new List<ZNetPeer> { serverPeer }, BuildSnapshotPackage(snap, false));
            for (int step = 0; step < 1000 && routine.MoveNext(); step++) { }
        }

        private static LeaderboardEntry BuildLocalSnapshot()
        {
            Player player = Player.m_localPlayer;
            if (player == null) { return null; }
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            if (profile == null || profile.m_playerStats == null) { return null; }

            // m_playerStats is bucketed by achievement difficulty; slot c_RawStats
            // is the unconditional lifetime total across every difficulty.
            PlayerProfile.PlayerStats rawStats = profile.m_playerStats[PlayerProfile.c_RawStats];
            if (rawStats == null) { return null; }
            var stats = rawStats.m_stats;

            StoreNeverDiedLife(player);
            long persistedDamage = GetLongKey(player, LeaderboardDamageKey);
            if (sessionDamage > 0) {
                persistedDamage += sessionDamage;
                SetLongKey(player, LeaderboardDamageKey, persistedDamage);
                sessionDamage = 0;
            }

            return new LeaderboardEntry {
                PlayerID = player.GetPlayerID(),
                PlayerName = profile.GetName(),
                DeathChoice = DeathConfigurationData.playerDeathConfiguration?.DisplayName ?? "",
                FirstLifeSeconds = GetLongKey(player, LeaderboardFirstLifeKey),
                LongestLifeSeconds = GetLongKey(player, LeaderboardLongestLifeKey),
                TotalLifeSeconds = GetLongKey(player, LeaderboardTotalLifeKey),
                DeathCount = (int)GetLongKey(player, LeaderboardDeathCountKey),
                TotalDamage = persistedDamage,
                BossKills = (int)stats[PlayerStatType.BossKills],
                TreeChops = (int)stats[PlayerStatType.TreeChops],
                Mines = (int)stats[PlayerStatType.Mines],
                CraftsAndBuilds = (int)(stats[PlayerStatType.CraftsOrUpgrades] + stats[PlayerStatType.Builds]),
            };
        }

        public static IEnumerator SyncLoop()
        {
            // Let the world finish loading before the first tick.
            yield return new WaitForSeconds(60f);
            while (true) {
                float intervalSeconds = Mathf.Max(60f, ValConfig.LeaderboardSyncInterval.Value * 60f);
                if (ValConfig.EnableLeaderboard.Value && ZNet.instance != null) {
                    try {
                        if (ZNet.instance.IsServer()) {
                            if (Player.m_localPlayer != null) { SendLocalSnapshotToServer(); }
                            SaveIfChanged();
                            BroadcastFullBoard();
                        } else if (Player.m_localPlayer != null) {
                            SendLocalSnapshotToServer();
                        }
                    } catch (Exception e) {
                        Logger.LogWarning($"Leaderboard sync tick failed: {e}");
                    }
                }
                yield return new WaitForSeconds(intervalSeconds);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
        public static class Leaderboard_OnDeath_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(Player __instance)
            {
                if (!ValConfig.EnableLeaderboard.Value) { return; }
                if (__instance == null || __instance != Player.m_localPlayer) { return; }

                long life;
                if (NeverDied(__instance)) {
                    life = NeverDiedLifeSeconds(__instance);
                    __instance.RemoveUniqueKeyValue(NeverDiedLifeKey);
                } else {
                    life = (long)Mathf.Max(0f, __instance.m_timeSinceDeath);
                }
                long deaths = GetLongKey(__instance, LeaderboardDeathCountKey);
                long total = GetLongKey(__instance, LeaderboardTotalLifeKey);
                long longest = GetLongKey(__instance, LeaderboardLongestLifeKey);

                if (deaths == 0) { SetLongKey(__instance, LeaderboardFirstLifeKey, life); }
                SetLongKey(__instance, LeaderboardDeathCountKey, deaths + 1);
                SetLongKey(__instance, LeaderboardTotalLifeKey, total + life);
                if (life > longest) { SetLongKey(__instance, LeaderboardLongestLifeKey, life); }
            }
        }

        // Character.Damage runs on the attacker's machine (it then sends RPC_Damage to the target's
        // owner), so this sees every hit the local player lands. It counts damage before resistances.
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Leaderboard_Damage_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Character __instance, HitData hit)
            {
                if (!ValConfig.EnableLeaderboard.Value) { return; }
                if (hit == null || Player.m_localPlayer == null) { return; }
                if (__instance is Player) { return; } // ignore PvP / self damage
                if (hit.GetAttacker() != Player.m_localPlayer) { return; }

                long dmg = (long)Mathf.Max(0f, hit.GetTotalDamage());
                if (dmg > 0) { sessionDamage += dmg; }
            }
        }

        // So saved stats reach the server without waiting for the slow sync tick.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Leaderboard_OnSpawned_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                if (!ValConfig.EnableLeaderboard.Value || __instance != Player.m_localPlayer) { return; }
                lifeClockStart = Time.time;
                if (initialReportSent) { return; }
                initialReportSent = true;
                try {
                    SendLocalSnapshotToServer();
                } catch (Exception e) {
                    Logger.LogWarning($"Initial leaderboard report failed: {e}");
                }
            }
        }

        // Game.Shutdown runs for both logout and quit, before ZNet closes the connection, so a session
        // shorter than the sync interval still reaches the server.
        [HarmonyPatch(typeof(Game), "Shutdown")]
        public static class Leaderboard_Shutdown_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                if (ValConfig.EnableLeaderboard.Value) {
                    try {
                        // Before the character is saved, which Game.Shutdown does next.
                        StoreNeverDiedLife(Player.m_localPlayer);
                        FlushLocalSnapshotToServer();
                    } catch (Exception e) {
                        Logger.LogWarning($"Leaderboard disconnect flush failed: {e}");
                    }
                }
                // These statics outlive the session, so a reconnect without restarting the game would
                // otherwise skip the spawn report.
                initialReportSent = false;
                sessionDamage = 0;
                lifeClockStart = -1f;
                if (ZNet.instance != null && ZNet.instance.IsServer()) { SaveIfChanged(); }
                // A client holds the last server's board in memory. Without this, hosting a world next
                // would merge that board into this machine's own file.
                leaderboard = new Dictionary<long, LeaderboardEntry>();
                Init();
            }
        }

        private static bool NeverDied(Player player)
        {
            return player.m_timeSinceDeath >= NeverDiedTime;
        }

        private static long NeverDiedLifeSeconds(Player player)
        {
            long session = lifeClockStart >= 0f ? (long)(Time.time - lifeClockStart) : 0L;
            return GetLongKey(player, NeverDiedLifeKey) + session;
        }

        // Moves this session's count onto the character, so it is saved with it.
        private static void StoreNeverDiedLife(Player player)
        {
            if (player == null || lifeClockStart < 0f || !NeverDied(player)) { return; }
            SetLongKey(player, NeverDiedLifeKey, NeverDiedLifeSeconds(player));
            lifeClockStart = Time.time;
        }

        private static long GetLongKey(Player player, string key)
        {
            if (player.TryGetUniqueKeyValue(key, out string raw) && long.TryParse(raw, out long val)) {
                return val;
            }
            return 0L;
        }

        private static void SetLongKey(Player player, string key, long value)
        {
            player.AddUniqueKeyValue(key, value.ToString());
        }
    }
}
