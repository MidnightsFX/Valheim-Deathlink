using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Deathlink.Death
{
    /// <summary>
    /// Lets the player hand-pick what survives a death, for two death styles:
    /// <list type="bullet">
    /// <item><c>DeathlinkBased</c> with choices on. Items are captured (and removed from the
    /// inventory) when the player dies. Chosen items are returned to the player, the remaining
    /// keep-budget is filled with random unpicked items, and the rest are lost.</item>
    /// <item><c>ChooseEquipment</c>. Everything goes in the tombstone when the player dies. Chosen
    /// equipment is pulled back out of the tombstone; the rest stays there.</item>
    /// </list>
    /// </summary>
    internal static class DeathSavingOptions
    {
        private enum ChoiceKind { KeepItems, TombstoneEquipment }

        // Crosses the wire as an int, so only ever append.
        private enum PullStatus { Pulled, InUse, Gone, NotYours, Unreadable }

        // For KeepItems these are the originals: the ItemData objects outlive the destroyed player
        // GameObject, so re-adding them to the respawned inventory restores them intact. For
        // TombstoneEquipment they are display copies; the real items are in the tombstone.
        private static readonly List<ItemDrop.ItemData> deathItems = new List<ItemDrop.ItemData>();
        // Picks plus random fill.
        private static int totalToSave = 0;
        private static int maxChoices = 0;
        private static bool pendingChoice = false;
        private static ChoiceKind pendingKind = ChoiceKind.KeepItems;
        // These statics outlive a logout, so without this the next character loaded would be offered,
        // and handed, the previous one's items.
        private static long choiceOwnerId = 0L;
        private const string PendingKeepKey = "DL_PendingKeepChoice";

        private static ZDOID choiceTombstone = ZDOID.None;
        // Each display copy's slot in the tombstone, which is how the real item is found again. Kept
        // apart from the copies because the selection panel lays them out through m_gridPos.
        private static readonly Dictionary<ItemDrop.ItemData, Vector2i> tombstoneSlots = new Dictionary<ItemDrop.ItemData, Vector2i>();
        // GUID-prefixed: every connection has one table of RPC name hashes, shared with vanilla and every
        // other mod, and a second Register under the same hash silently replaces the first.
        private const string PullRequestRpc = Deathlink.PluginGUID + ".GravePullRequest";
        private const string PullResultRpc = Deathlink.PluginGUID + ".GravePullResult";
        // Never reset, so a reply to an earlier request can always be told apart from the current one.
        private static int pullRequest = 0;
        private static int pullRequestedCount = 0;
        private static bool awaitingPull = false;
        private static float pullSentAt = 0f;
        private const float PullTimeoutSeconds = 10f;
        // Pulled gear that has not reached an inventory yet: a reply can land while the player is dead.
        private static readonly List<ItemDrop.ItemData> undelivered = new List<ItemDrop.ItemData>();

        public static void CaptureDeathChoice(Player player, List<ItemDrop.ItemData> candidateItems, int totalKept, int choiceCount)
        {
            ResetChoice();
            deathItems.AddRange(candidateItems);
            totalToSave = Mathf.Max(0, totalKept);
            maxChoices = Mathf.Clamp(choiceCount, 0, totalToSave);
            pendingKind = ChoiceKind.KeepItems;
            choiceOwnerId = LocalPlayerId();
            pendingChoice = true;
            SaveKeepChoice(player);
            Logger.LogDebug($"Captured {deathItems.Count} death items for choice, keep {totalToSave}, pick up to {maxChoices}.");
        }

        // The candidates were taken out of the inventory at death and exist nowhere else, so they are
        // also written to the character: a quit or crash before Confirm would otherwise lose them all.
        private static void SaveKeepChoice(Player player)
        {
            if (player == null) { return; }
            player.m_customData[PendingKeepKey] = $"{totalToSave};{maxChoices};{Convert.ToBase64String(SerializeItems(deathItems))}";
        }

        private static void ClearSavedKeepChoice()
        {
            if (Player.m_localPlayer != null) { Player.m_localPlayer.m_customData.Remove(PendingKeepKey); }
        }

        // Every spawn loads the character again, so this also runs after each respawn and simply
        // rebuilds the same choice from the saved copy.
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        public static class RestoreKeepChoice
        {
            public static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) { return; }
                if (!__instance.m_customData.TryGetValue(PendingKeepKey, out string saved)) { return; }
                string[] parts = saved.Split(new[] { ';' }, 3);
                Inventory items = null;
                if (parts.Length == 3 && int.TryParse(parts[0], out int total) && int.TryParse(parts[1], out int choices)) {
                    try {
                        items = LoadDetached(Convert.FromBase64String(parts[2]), requireAll: false);
                    } catch (FormatException) { }
                    if (items != null) {
                        ResetChoice();
                        deathItems.AddRange(items.m_inventory);
                        totalToSave = total;
                        maxChoices = choices;
                        pendingKind = ChoiceKind.KeepItems;
                        choiceOwnerId = __instance.GetPlayerID();
                        pendingChoice = true;
                        Logger.LogDebug($"Restored an unfinished death item choice of {deathItems.Count} items.");
                        return;
                    }
                }
                Logger.LogWarning("Could not read this character's unfinished death item choice, it was dropped.");
                __instance.m_customData.Remove(PendingKeepKey);
            }
        }

        // A second death before the first choice was made: resolve that one at random, as if Confirm
        // had been pressed with nothing picked, so this death decides those items along with the rest.
        internal static void ResolveUnchosenKeepItems()
        {
            if (ChoiceIsForLocalPlayer() && pendingKind == ChoiceKind.KeepItems) { ApplySelection(null); }
        }

        // Called once the equipment is in the tombstone, so each piece's m_gridPos is its slot there.
        public static void CaptureEquipmentChoice(ZDOID tombstone, long ownerId, List<ItemDrop.ItemData> equipment, int choiceCount)
        {
            ResetChoice();
            foreach (ItemDrop.ItemData item in equipment) {
                ItemDrop.ItemData copy = item.Clone();
                deathItems.Add(copy);
                tombstoneSlots[copy] = item.m_gridPos;
            }
            maxChoices = Mathf.Clamp(choiceCount, 0, deathItems.Count);
            totalToSave = maxChoices;
            choiceTombstone = tombstone;
            choiceOwnerId = ownerId;
            pendingKind = ChoiceKind.TombstoneEquipment;
            pendingChoice = true;
            Logger.LogDebug($"Captured {deathItems.Count} equipment in tombstone {tombstone} for choice, pick up to {maxChoices}.");
        }

        private static void ResetChoice()
        {
            if (pendingChoice && pendingKind == ChoiceKind.KeepItems && choiceOwnerId == LocalPlayerId()) { ClearSavedKeepChoice(); }
            deathItems.Clear();
            tombstoneSlots.Clear();
            totalToSave = 0;
            maxChoices = 0;
            pendingChoice = false;
            choiceOwnerId = 0L;
            choiceTombstone = ZDOID.None;
            awaitingPull = false;
        }

        private static long LocalPlayerId()
        {
            PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            return profile != null ? profile.GetPlayerID() : 0L;
        }

        private static bool ChoiceIsForLocalPlayer()
        {
            return pendingChoice && choiceOwnerId == LocalPlayerId();
        }

        private static bool IsChoiceTombstone(Container container)
        {
            if (container == null) { return false; }
            ZNetView nview = container.GetComponent<ZNetView>();
            return nview != null && nview.GetZDO() != null && nview.GetZDO().m_uid == choiceTombstone;
        }

        // On respawn, nudge the player to open their inventory instead of forcing a panel up at the
        // exact moment control returns (where the input block never took hold reliably).
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Player_OnSpawned_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (__instance == null || __instance != Player.m_localPlayer) { return; }
                DeliverPulledItems();
                if (!ChoiceIsForLocalPlayer()) { return; }
                ShowMessage(pendingKind == ChoiceKind.TombstoneEquipment ? "$dso_equip_spawn_message" : "$dso_spawn_message");
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class ShowSaveChoiceUI
        {
            public static void Postfix(Container container)
            {
                if (!ChoiceIsForLocalPlayer()) { return; }
                bool fromTombstone = pendingKind == ChoiceKind.TombstoneEquipment;
                if (fromTombstone) {
                    // This client drops a tombstone's ZDO once it is destroyed, which is what happens when
                    // the player runs back and loots all of it. There is nothing left to choose from then.
                    if (ZDOMan.instance == null || ZDOMan.instance.GetZDO(choiceTombstone) == null) {
                        Logger.LogDebug("The tombstone for the pending equipment choice is gone, dropping the choice.");
                        ResetChoice();
                        return;
                    }
                    // Looting the tombstone directly. Picking from it at the same time would only fight
                    // over the same items.
                    if (IsChoiceTombstone(container)) {
                        if (SaveChoiceUI.IsInitialized) { SaveChoiceUI.Instance.Hide(); }
                        return;
                    }
                    if (awaitingPull) {
                        if (Time.realtimeSinceStartup - pullSentAt < PullTimeoutSeconds) { return; }
                        // No reply. Let them send again; a late reply still delivers whatever it pulled.
                        awaitingPull = false;
                    }
                }
                SaveChoiceUI.Instance.Show(new List<ItemDrop.ItemData>(deathItems), totalToSave, maxChoices, fromTombstone);
            }
        }

        // The choice stays pending, so the panel comes back on every inventory open until Confirm.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        public static class HideSaveChoiceUI
        {
            public static void Postfix()
            {
                if (SaveChoiceUI.IsInitialized) { SaveChoiceUI.Instance.Hide(); }
            }
        }

        // False keeps the panel open, for a tombstone pull that cannot be sent yet.
        private static bool ResolveSelection(List<ItemDrop.ItemData> picked)
        {
            if (pendingKind == ChoiceKind.TombstoneEquipment) { return RequestTombstonePull(picked); }
            ApplySelection(picked);
            return true;
        }

        private static void ApplySelection(List<ItemDrop.ItemData> picked)
        {
            Player player = Player.m_localPlayer;
            List<ItemDrop.ItemData> saved = new List<ItemDrop.ItemData>();
            if (picked != null) {
                foreach (var item in picked) {
                    if (deathItems.Contains(item) && !saved.Contains(item)) { saved.Add(item); }
                }
            }

            if (saved.Count < totalToSave) {
                List<ItemDrop.ItemData> remainder = Deathlink.shuffleList(deathItems.Where(i => !saved.Contains(i)).ToList());
                foreach (var item in remainder) {
                    if (saved.Count >= totalToSave) { break; }
                    saved.Add(item);
                }
            }

            if (player != null) {
                foreach (var item in saved) { GiveOrDrop(player, item); }
                Logger.LogDebug($"Returned {saved.Count} items to the player after death choice.");
            } else {
                Logger.LogWarning("Local player missing while applying death choice, saved items were lost.");
            }

            ResetChoice();
        }

        // The items may still carry the equipped flag from the pre-death player, which the respawned
        // player does not reference, so it is cleared to avoid an "equipped but inactive" item.
        private static void GiveOrDrop(Player player, ItemDrop.ItemData item)
        {
            item.m_equipped = false;
            if (player.GetInventory().AddItem(item)) { return; }
            // Only when the inventory filled up after the choice was made.
            ItemDrop.DropItem(item, 0, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
        }

        // The pull runs where the authoritative copy of the tombstone lives (the server, or this machine
        // when it is one): this client's copy goes stale the moment anyone else touches the tombstone, and
        // editing a stale copy either loses the edit or overwrites theirs.
        //
        // Both hops use the peer connection itself (ZNetPeer.m_rpc), never ZRoutedRpc, which is what
        // Jotunn's CustomRPC is built on. A routed RPC's sender id is written by the sending client and
        // can be addressed to any peer, so another client could hand a player items as if the server had,
        // or have the reply go elsewhere. A ZRpc handler is bound to the socket the call arrived on. Which
        // grave a client may pull from still rests on the player id on its own character, which a modified
        // client controls -- no worse than vanilla, where anyone can open any tombstone.

        // ZNet registers its own per-connection RPCs here, on both ends of every connection: on the
        // server once per client, and on a client once, for its link to the server.
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        public static class RegisterPullRpcs
        {
            public static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (__instance.IsServer()) {
                    peer.m_rpc.Register<ZPackage>(PullRequestRpc, OnServerReceivePull);
                } else {
                    peer.m_rpc.Register<ZPackage>(PullResultRpc, OnClientReceivePull);
                }
            }
        }

        private static bool RequestTombstonePull(List<ItemDrop.ItemData> picked)
        {
            Player player = Player.m_localPlayer;
            List<ItemDrop.ItemData> picks = (picked ?? new List<ItemDrop.ItemData>())
                .Where(item => tombstoneSlots.ContainsKey(item)).Distinct().Take(maxChoices).ToList();
            if (picks.Count == 0) {
                ShowMessage("$dso_equip_left");
                ResetChoice();
                return true;
            }
            if (player == null || ZNet.instance == null || ZDOMan.instance == null) { return false; }
            // Checked here, before anything leaves the tombstone, so the gear is never left with nowhere to go.
            if (player.GetInventory().GetEmptySlots() < picks.Count) {
                player.Message(MessageHud.MessageType.Center,
                    string.Format(Localization.instance.Localize("$dso_equip_needspace"), picks.Count));
                return false;
            }

            List<ItemDrop.ItemData> keys = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData pick in picks) {
                ItemDrop.ItemData key = pick.Clone();
                key.m_gridPos = tombstoneSlots[pick];
                keys.Add(key);
            }
            byte[] pickData = SerializeItems(keys);
            pullRequest++;
            pullRequestedCount = picks.Count;

            if (ZNet.instance.IsServer()) {
                List<ItemDrop.ItemData> pulled = new List<ItemDrop.ItemData>();
                PullStatus status = PullFromTombstone(choiceTombstone, LocalPlayerId(), pickData, pulled, out _);
                ApplyPullResult(pullRequest, status, pulled);
                return true;
            }

            ZRpc server = ZNet.instance.GetServerRPC();
            if (server == null) { return false; }
            ZPackage package = new ZPackage();
            package.Write(pullRequest);
            package.Write(choiceTombstone);
            package.Write(pickData);
            server.Invoke(PullRequestRpc, package);
            awaitingPull = true;
            pullSentAt = Time.realtimeSinceStartup;
            return true;
        }

        // Server side (or the host). pulledData is the pulled items already serialized: it is built
        // before the tombstone is written, so nothing can fail between the items leaving the tombstone
        // and the reply that carries them.
        private static PullStatus PullFromTombstone(ZDOID tombstoneId, long requesterId, byte[] pickData,
            List<ItemDrop.ItemData> pulled, out byte[] pulledData)
        {
            pulledData = SerializeItems(pulled);
            ZDO tombstone = ZDOMan.instance.GetZDO(tombstoneId);
            if (tombstone == null) { return PullStatus.Gone; }
            if (requesterId == 0L || tombstone.GetLong(ZDOVars.s_owner, 0L) != requesterId) { return PullStatus.NotYours; }
            // Someone has it open, and their container would save over this edit when they are done.
            if (tombstone.GetInt(ZDOVars.s_inUse) == 1) { return PullStatus.InUse; }
            byte[] contentsData = tombstone.GetByteArray(ZDOVars.s_items);
            if (contentsData == null) { return PullStatus.Gone; }

            Inventory contents = LoadDetached(contentsData, requireAll: true);
            Inventory picks = LoadDetached(pickData, requireAll: true);
            if (contents == null || picks == null) { return PullStatus.Unreadable; }

            // Same slot first, then anywhere: the tombstone may have been rearranged since the death.
            List<ItemDrop.ItemData> unmatched = new List<ItemDrop.ItemData>(picks.m_inventory);
            foreach (bool sameSlot in new[] { true, false }) {
                foreach (ItemDrop.ItemData pick in unmatched.ToList()) {
                    ItemDrop.ItemData match = contents.m_inventory.FirstOrDefault(item =>
                        (sameSlot == false || (item.m_gridPos.x == pick.m_gridPos.x && item.m_gridPos.y == pick.m_gridPos.y))
                        && SameItem(item, pick));
                    if (match == null) { continue; }
                    contents.RemoveItem(match);
                    pulled.Add(match);
                    unmatched.Remove(pick);
                }
            }
            if (pulled.Count == 0) { return PullStatus.Pulled; }

            pulledData = SerializeItems(pulled);
            tombstone.Set(ZDOVars.s_items, SerializeItems(contents.m_inventory));
            Logger.LogDebug($"Pulled {pulled.Count} of {picks.m_inventory.Count} picked items from tombstone {tombstoneId}.");
            return PullStatus.Pulled;
        }

        private static void OnServerReceivePull(ZRpc rpc, ZPackage package)
        {
            ZNetPeer requester = ZNet.instance.GetPeer(rpc);
            if (requester == null) { return; }
            try {
                int request = package.ReadInt();
                ZDOID tombstone = package.ReadZDOID();
                byte[] pickData = package.ReadByteArray();
                PullStatus status = PullFromTombstone(tombstone, Common.Utils.PlayerIdOfPeer(requester), pickData,
                    new List<ItemDrop.ItemData>(), out byte[] pulledData);
                ZPackage reply = new ZPackage();
                reply.Write(request);
                reply.Write((int)status);
                reply.Write(pulledData);
                rpc.Invoke(PullResultRpc, reply);
            } catch (Exception e) {
                Logger.LogWarning($"Failed to handle a tombstone pull from {requester.m_playerName} ({requester.m_uid}): {e}");
            }
        }

        // Registered only on the connection to the server, so it can only ever come from there.
        private static void OnClientReceivePull(ZRpc rpc, ZPackage package)
        {
            try {
                int request = package.ReadInt();
                PullStatus status = (PullStatus)package.ReadInt();
                // Not requireAll: these have already left the tombstone, so anything readable is handed over.
                Inventory pulled = LoadDetached(package.ReadByteArray(), requireAll: false);
                ApplyPullResult(request, status, pulled != null ? pulled.m_inventory.ToList() : new List<ItemDrop.ItemData>());
            } catch (Exception e) {
                Logger.LogWarning($"Failed to read the tombstone pull reply: {e}");
            }
        }

        private static void ApplyPullResult(int request, PullStatus status, List<ItemDrop.ItemData> pulled)
        {
            // Whatever was pulled has already left the tombstone, so it is handed over even when the
            // reply belongs to an earlier request that timed out.
            undelivered.AddRange(pulled);
            DeliverPulledItems();

            if (request != pullRequest || !pendingChoice || pendingKind != ChoiceKind.TombstoneEquipment) { return; }
            awaitingPull = false;
            Logger.LogDebug($"Tombstone pull {request}: {status}, {pulled.Count} of {pullRequestedCount} picked items.");

            switch (status) {
                case PullStatus.Pulled:
                    ShowMessage(pulled.Count < pullRequestedCount ? "$dso_equip_partial" : "$dso_equip_recovered");
                    break;
                case PullStatus.InUse:
                    // The one outcome worth retrying, so the choice stays up for the next inventory open.
                    ShowMessage("$dso_equip_inuse");
                    return;
                case PullStatus.Gone:
                    ShowMessage("$dso_equip_gone");
                    break;
                default:
                    Logger.LogWarning($"Could not take the picked equipment from tombstone {choiceTombstone}: {status}. It was left there.");
                    ShowMessage("$dso_equip_failed");
                    break;
            }
            ResetChoice();
        }

        private static void DeliverPulledItems()
        {
            Player player = Player.m_localPlayer;
            // Added to a dying player they would vanish with the body, so wait for the respawn.
            if (player == null || player.IsDead() || undelivered.Count == 0) { return; }
            foreach (ItemDrop.ItemData item in undelivered) { GiveOrDrop(player, item); }
            Logger.LogDebug($"Handed over {undelivered.Count} items pulled from the tombstone.");
            undelivered.Clear();
        }

        // Durability is left out: a save rounds it, and the fields below already tell pieces apart.
        private static bool SameItem(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return a.m_shared.m_name == b.m_shared.m_name
                && a.m_quality == b.m_quality
                && a.m_variant == b.m_variant
                && a.m_stack == b.m_stack
                && a.m_crafterID == b.m_crafterID
                && a.m_worldLevel == b.m_worldLevel
                && a.m_customData.Count == b.m_customData.Count
                && a.m_customData.All(pair => b.m_customData.TryGetValue(pair.Key, out string value) && value == pair.Value);
        }

        // Wide enough for any slot a save can name (x is written as a byte); Load skips the height check.
        private static Inventory DetachedInventory()
        {
            return new Inventory("DL_Detached", null, 256, 1);
        }

        private static byte[] SerializeItems(List<ItemDrop.ItemData> items)
        {
            Inventory holder = DetachedInventory();
            holder.m_inventory.AddRange(items);
            ZPackage package = new ZPackage();
            holder.Save(package);
            return package.GetArray();
        }

        // With requireAll, null unless every item came back. An item whose prefab this machine does not
        // have is silently skipped by Load, and saving that inventory again would delete it for good.
        private static Inventory LoadDetached(byte[] data, bool requireAll)
        {
            if (data == null || data.Length == 0) { return null; }
            try {
                ZPackage header = new ZPackage(data);
                int version = header.ReadInt();
                int expected = version >= (int)global::Version.Item.Smaller ? header.ReadUShort() : header.ReadInt();
                Inventory inventory = DetachedInventory();
                inventory.Load(new ZPackage(data));
                if (requireAll && inventory.NrOfItems() != expected) {
                    Logger.LogWarning($"Only {inventory.NrOfItems()} of {expected} items could be read, so nothing was changed.");
                    return null;
                }
                return inventory;
            } catch (Exception e) {
                Logger.LogWarning($"Could not read an item list: {e.Message}");
                return null;
            }
        }

        private static void ShowMessage(string key)
        {
            Player player = Player.m_localPlayer;
            if (player != null) { player.Message(MessageHud.MessageType.Center, Localization.instance.Localize(key)); }
        }

        /// <summary>
        /// Shows the death items in a clone of a vanilla <see cref="InventoryGrid"/>, so slots get the
        /// standard icons and tooltips, and slot-decorating mods (EpicLoot patches
        /// <c>InventoryGrid.UpdateGui</c>) style them as in the real inventory.
        /// </summary>
        public class SaveChoiceUI : MonoBehaviour
        {
            public static SaveChoiceUI Instance {
                get {
                    if (_instance == null) {
                        GameObject holder = new GameObject("DeathlinkSaveChoiceUI");
                        DontDestroyOnLoad(holder);
                        _instance = holder.AddComponent<SaveChoiceUI>();
                    }
                    return _instance;
                }
            }
            private static SaveChoiceUI _instance;
            public static bool IsInitialized => _instance != null;

            // Standard player-inventory row width, so slots render at the familiar size/scale.
            private const int GridWidth = 8;

            private static GameObject panel;
            private static Text headerText;
            private static Text descriptionText;
            private static Text counterText;

            private static InventoryGrid grid;
            private static RectTransform gridRoot;
            private static Inventory tempInv;

            private static readonly HashSet<ItemDrop.ItemData> selected = new HashSet<ItemDrop.ItemData>();
            private static int keepBudget = 0;
            private static int pickLimit = 0;
            private static bool fillsAtRandom = true;

            public void Awake()
            {
                _instance = this;
            }

            // Re-drive the grid every frame while open so hover tooltips stay live and slot-decorating
            // mods re-apply, exactly like InventoryGui does for the real grids.
            public void Update()
            {
                if (panel == null || !panel.activeSelf || grid == null || tempInv == null) { return; }
                // A null player hides the hotbar binding numbers and equipped markers.
                grid.UpdateInventory(tempInv, null, null);
            }

            public void Show(List<ItemDrop.ItemData> items, int totalKept, int choiceCount, bool fromTombstone)
            {
                if (!EnsurePanel()) { return; }
                if (!EnsureGrid()) { return; }
                keepBudget = totalKept;
                pickLimit = choiceCount;
                fillsAtRandom = fromTombstone == false;
                headerText.text = Localization.instance.Localize(fromTombstone ? "$dso_equip_header" : "$dso_header");
                descriptionText.text = Localization.instance.Localize(fromTombstone ? "$dso_equip_description" : "$dso_description");
                selected.Clear();
                BuildInventory(items);
                grid.UpdateInventory(tempInv, null, null);
                grid.ResetView();
                ClearHighlights();
                UpdateCounter();
                panel.SetActive(true);
            }

            public void Hide()
            {
                if (panel != null) { panel.SetActive(false); }
            }

            private void Confirm()
            {
                List<ItemDrop.ItemData> picks = selected.ToList();
                if (ResolveSelection(picks) == false) { return; }
                Hide();
                selected.Clear();
            }

            private bool EnsurePanel()
            {
                if (panel != null) { return true; }
                if (GUIManager.Instance == null || !GUIManager.CustomGUIFront) {
                    Logger.LogWarning("GUIManager not setup, skipping death saving panel creation.");
                    return false;
                }

                panel = GUIManager.Instance.CreateWoodpanel(
                    parent: GUIManager.CustomGUIFront.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, 0),
                    width: 700,
                    height: 640,
                    draggable: true);
                panel.SetActive(false);

                // Show sets the real header and description text: the panel serves both death styles.
                headerText = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$dso_header"),
                    parent: panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, 270f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 30,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 620f,
                    height: 40f,
                    addContentSizeFitter: false).GetComponent<Text>();

                descriptionText = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$dso_description"),
                    parent: panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, 225f),
                    font: GUIManager.Instance.AveriaSerif,
                    fontSize: 18,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 620f,
                    height: 50f,
                    addContentSizeFitter: false).GetComponent<Text>();
                descriptionText.alignment = TextAnchor.MiddleCenter;

                counterText = GUIManager.Instance.CreateText(
                    text: "",
                    parent: panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, 180f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 18,
                    color: GUIManager.Instance.ValheimYellow,
                    outline: true,
                    outlineColor: Color.black,
                    width: 620f,
                    height: 40f,
                    addContentSizeFitter: false).GetComponent<Text>();
                counterText.alignment = TextAnchor.MiddleCenter;

                var confirmButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$dso_confirm"),
                    parent: panel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, -270f),
                    width: 250f,
                    height: 60f);
                confirmButton.GetComponent<Button>().onClick.AddListener(Confirm);

                return true;
            }

            // Clones the container grid because, unlike the player grid, it has no hotbar binding row.
            private bool EnsureGrid()
            {
                if (grid != null) { return true; }
                if (InventoryGui.instance == null || InventoryGui.instance.ContainerGrid == null) {
                    Logger.LogWarning("InventoryGui not ready, cannot build death saving grid.");
                    return false;
                }

                // A fixed-size holder gives the grid a concrete rect to fill. Cloning only the grid
                // drops the scroll viewport that normally sizes it; changing its anchors without a
                // size collapses the RectTransform (and any mask on it) to 0x0, so nothing renders.
                var holderGo = new GameObject("DL_GridHolder", typeof(RectTransform));
                holderGo.transform.SetParent(panel.transform, false);
                var holder = (RectTransform)holderGo.transform;
                holder.anchorMin = new Vector2(0.5f, 0.5f);
                holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.sizeDelta = new Vector2(600f, 380f);
                holder.anchoredPosition = new Vector2(0f, -40f);

                GameObject source = InventoryGui.instance.ContainerGrid.gameObject;
                GameObject clone = UnityEngine.Object.Instantiate(source, holder);
                clone.name = "DL_SaveGrid";
                clone.SetActive(true);

                var rt = (RectTransform)clone.transform;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                grid = clone.GetComponent<InventoryGrid>();
                gridRoot = grid.m_gridRoot;
                // Force a clean element rebuild on the first UpdateGui and drop any stale slot GOs the
                // source grid may have had from a previously opened container.
                if (gridRoot != null) {
                    gridRoot.gameObject.SetActive(true);
                    foreach (Transform child in gridRoot) { Destroy(child.gameObject); }
                }
                grid.m_width = 0;
                grid.m_height = 0;

                // Wired on every new clone: the panel lives under CustomGUIFront, which is rebuilt with
                // each world load, and Instantiate does not copy runtime delegates.
                grid.m_onSelected += (g, item, pos, mod) => OnItemClicked(item, pos);
                // Both are called without a null check: m_onRightClick on a right-click, and
                // CanDropDragOntoItem by UpdateGui for every item, so a null one throws on the first
                // non-empty refresh. Nothing is draggable in this panel, so nothing accepts a drop.
                grid.m_onRightClick += (g, item, pos) => { };
                grid.CanDropDragOntoItem = item => false;
                return true;
            }

            // Added to the backing list directly rather than through AddItem, so stacks are never merged
            // and the selection keeps the exact ItemData references.
            private void BuildInventory(List<ItemDrop.ItemData> items)
            {
                int count = Mathf.Max(1, items.Count);
                int height = Mathf.Max(1, Mathf.CeilToInt(count / (float)GridWidth));
                tempInv = new Inventory("DL_SaveChoice", null, GridWidth, height);
                tempInv.m_inventory.Clear();
                int i = 0;
                foreach (var item in items) {
                    item.m_gridPos = new Vector2i(i % GridWidth, i / GridWidth);
                    tempInv.m_inventory.Add(item);
                    i++;
                }
                tempInv.Changed();
            }

            private void OnItemClicked(ItemDrop.ItemData item, Vector2i pos)
            {
                if (item == null) { return; }
                if (selected.Contains(item)) {
                    selected.Remove(item);
                    SetHighlight(pos, false);
                } else {
                    if (selected.Count >= pickLimit) { return; }
                    selected.Add(item);
                    SetHighlight(pos, true);
                }
                UpdateCounter();
            }

            // Slot elements are created row-major under gridRoot. UpdateGui never touches this overlay
            // child, so it survives the per-frame refresh.
            private void SetHighlight(Vector2i pos, bool on)
            {
                if (gridRoot == null) { return; }
                int idx = pos.y * GridWidth + pos.x;
                if (idx < 0 || idx >= gridRoot.childCount) { return; }
                Transform element = gridRoot.GetChild(idx);
                Transform hl = element.Find("dl_highlight");
                if (hl == null) {
                    var go = new GameObject("dl_highlight", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(element, false);
                    var hrt = (RectTransform)go.transform;
                    hrt.anchorMin = Vector2.zero;
                    hrt.anchorMax = Vector2.one;
                    hrt.offsetMin = Vector2.zero;
                    hrt.offsetMax = Vector2.zero;
                    var img = go.GetComponent<Image>();
                    img.color = new Color(1f, 0.82f, 0.2f, 0.4f);
                    img.raycastTarget = false; // let clicks fall through to the slot's input handler
                    hl = go.transform;
                }
                hl.gameObject.SetActive(on);
            }

            private void ClearHighlights()
            {
                if (gridRoot == null) { return; }
                foreach (Transform element in gridRoot) {
                    Transform hl = element.Find("dl_highlight");
                    if (hl != null) { hl.gameObject.SetActive(false); }
                }
            }

            private void UpdateCounter()
            {
                if (counterText == null) { return; }
                string line = string.Format(Localization.instance.Localize("$dso_selected"), selected.Count, pickLimit);
                int randomFill = fillsAtRandom ? Mathf.Max(0, keepBudget - selected.Count) : 0;
                if (randomFill > 0) {
                    line += "  -  " + string.Format(Localization.instance.Localize("$dso_random_note"), randomFill);
                }
                counterText.text = line;
            }
        }
    }
}
