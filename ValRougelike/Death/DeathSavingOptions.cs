using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Deathlink.Death
{
    /// <summary>
    /// Lets the player hand-pick which items survive a <c>DeathlinkBased</c> death. Items are
    /// captured (and removed from the inventory) when the player dies. On respawn the player gets a
    /// one-time message, and the selection panel is shown when they open their inventory (a context
    /// where input handling and the cursor already work, unlike the raw respawn moment). Chosen
    /// items are returned to the player; the remaining keep-budget is filled with random unpicked
    /// items.
    /// </summary>
    internal static class DeathSavingOptions
    {
        // Candidate items captured at death. Held as plain references across the death->respawn
        // boundary: the ItemData objects outlive the destroyed player GameObject, so re-adding
        // them to the freshly spawned inventory restores the originals intact.
        private static readonly List<ItemDrop.ItemData> deathItems = new List<ItemDrop.ItemData>();
        // Total items kept (picks + random fill); equals the DeathlinkBased "numberOfItemsSavable".
        private static int totalToSave = 0;
        // How many items the player may hand-pick (X), already clamped to [0, totalToSave].
        private static int maxChoices = 0;
        // True between death and the player resolving the selection panel.
        private static bool pendingChoice = false;

        /// <summary>
        /// Records the death inventory and the save budget so the selection panel can be shown when
        /// the player next opens their inventory. Called from <see cref="OnDeathChanges"/> after the
        /// candidate items have already been removed from the player's inventory.
        /// </summary>
        public static void CaptureDeathChoice(List<ItemDrop.ItemData> candidateItems, int totalKept, int choiceCount)
        {
            deathItems.Clear();
            deathItems.AddRange(candidateItems);
            totalToSave = Mathf.Max(0, totalKept);
            maxChoices = Mathf.Clamp(choiceCount, 0, totalToSave);
            pendingChoice = true;
            Logger.LogDebug($"Captured {deathItems.Count} death items for choice, keep {totalToSave}, pick up to {maxChoices}.");
        }

        // On respawn, nudge the player to open their inventory instead of forcing a panel up at the
        // exact moment control returns (where the input block never took hold reliably).
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class Player_OnSpawned_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (!pendingChoice) { return; }
                if (__instance == null || __instance != Player.m_localPlayer) { return; }
                __instance.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$dso_spawn_message"));
            }
        }

        // Show the panel when the inventory opens (mirrors DeathChoiceEnable.ShowDeathChoiceUI).
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class ShowSaveChoiceUI
        {
            public static void Postfix()
            {
                if (!pendingChoice) { return; }
                SaveChoiceUI.Instance.Show(new List<ItemDrop.ItemData>(deathItems), totalToSave, maxChoices);
            }
        }

        // Hide the panel when the inventory closes, but leave the choice pending so it re-appears on
        // the next open until the player confirms.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        public static class HideSaveChoiceUI
        {
            public static void Postfix()
            {
                if (SaveChoiceUI.IsInitialized) { SaveChoiceUI.Instance.Hide(); }
            }
        }

        /// <summary>
        /// Applies the player's selection: the picked items plus a random fill (from the unpicked
        /// remainder) up to the total keep-budget are returned to the player, the rest discarded.
        /// </summary>
        private static void ApplySelection(List<ItemDrop.ItemData> picked)
        {
            Player player = Player.m_localPlayer;
            List<ItemDrop.ItemData> saved = new List<ItemDrop.ItemData>();
            if (picked != null) {
                foreach (var item in picked) {
                    if (deathItems.Contains(item) && !saved.Contains(item)) { saved.Add(item); }
                }
            }

            // Fill any remaining budget with random items the player did not pick.
            if (saved.Count < totalToSave) {
                List<ItemDrop.ItemData> remainder = Deathlink.shuffleList(deathItems.Where(i => !saved.Contains(i)).ToList());
                foreach (var item in remainder) {
                    if (saved.Count >= totalToSave) { break; }
                    saved.Add(item);
                }
            }

            if (player != null) {
                foreach (var item in saved) {
                    // The captured items may still carry the equipped flag from the pre-death player;
                    // the respawned player does not reference them, so clear it to avoid a desynced
                    // "equipped but inactive" state. The player re-equips manually, like grave loot.
                    item.m_equipped = false;
                    player.m_inventory.AddItem(item);
                }
                Logger.LogDebug($"Returned {saved.Count} items to the player after death choice.");
            } else {
                Logger.LogWarning("Local player missing while applying death choice, saved items were lost.");
            }

            deathItems.Clear();
            totalToSave = 0;
            maxChoices = 0;
            pendingChoice = false;
        }

        /// <summary>
        /// Persistent overlay panel that shows the death inventory as a real Valheim item grid and
        /// lets the player toggle up to <c>maxChoices</c> items to keep. Items are rendered by a
        /// cloned <see cref="InventoryGrid"/> driven from a temporary <see cref="Inventory"/>, so the
        /// slots get their full standard icon/tooltip and any slot-decorating mod (e.g. EpicLoot,
        /// which patches <c>InventoryGrid.UpdateGui</c>) styles them exactly as in the real
        /// inventory. Mirrors <see cref="DeathChoices.DeathChoiceUI"/>: a DontDestroyOnLoad singleton
        /// so the panel survives world reloads and never duplicates.
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
            private static Text counterText;

            // Cloned vanilla grid + the temp inventory that backs it.
            private static InventoryGrid grid;
            private static RectTransform gridRoot;
            private static Inventory tempInv;
            private static bool selectionWired;

            private static readonly HashSet<ItemDrop.ItemData> selected = new HashSet<ItemDrop.ItemData>();
            private static int keepBudget = 0;
            private static int pickLimit = 0;

            public void Awake()
            {
                _instance = this;
            }

            // Re-drive the grid every frame while open so hover tooltips stay live and slot-decorating
            // mods re-apply, exactly like InventoryGui does for the real grids.
            public void Update()
            {
                if (panel == null || !panel.activeSelf) { return; }
                if (grid == null || tempInv == null) { return; }
                // player == null: suppresses the row-0 hotbar binding numbers and equipped markers,
                // which are meaningless for detached death loot. Tooltips/decoration don't need it.
                grid.UpdateInventory(tempInv, null, null);
            }

            public void Show(List<ItemDrop.ItemData> items, int totalKept, int choiceCount)
            {
                if (!EnsurePanel()) { return; }
                if (!EnsureGrid()) { return; }
                keepBudget = totalKept;
                pickLimit = choiceCount;
                selected.Clear();
                BuildInventory(items);
                grid.UpdateInventory(tempInv, null, null); // build/refresh slot elements now
                grid.ResetView();                          // position gridRoot like the vanilla container
                Logger.LogDebug($"SaveChoiceUI grid: {tempInv.GetWidth()}x{tempInv.GetHeight()}, " +
                    $"slot elements={(gridRoot != null ? gridRoot.childCount : -1)}, " +
                    $"gridRect={((RectTransform)grid.transform).rect.size}");
                ClearHighlights();
                UpdateCounter();
                panel.SetActive(true);
            }

            public void Hide()
            {
                // Only hides the overlay; the choice stays pending until Confirm resolves it, so it
                // re-appears the next time the inventory is opened.
                if (panel != null) { panel.SetActive(false); }
            }

            private void Confirm()
            {
                List<ItemDrop.ItemData> picks = selected.ToList();
                Hide();
                selected.Clear();
                ApplySelection(picks);
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

                GUIManager.Instance.CreateText(
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
                    addContentSizeFitter: false);

                var desc = GUIManager.Instance.CreateText(
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
                    addContentSizeFitter: false);
                desc.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

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

            // Clones a fully-wired vanilla grid (the container grid: no hotbar binding row) into the
            // panel and hooks click-to-select. The clone keeps the serialized wiring (element prefab,
            // grid root, ui group, tooltip anchor); the runtime m_onSelected delegate is not
            // serialized, so we add our own.
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
                GameObject clone = Object.Instantiate(source, holder);
                clone.name = "DL_SaveGrid";
                clone.SetActive(true);

                // Stretch-fill the holder so the grid's rect matches it (mimics the vanilla viewport).
                var rt = (RectTransform)clone.transform;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                grid = clone.GetComponent<InventoryGrid>();
                gridRoot = grid.m_gridRoot;
                if (gridRoot != null) { gridRoot.gameObject.SetActive(true); }

                // Force a clean element rebuild on the first UpdateGui and drop any stale slot GOs the
                // source grid may have had from a previously opened container.
                if (gridRoot != null) {
                    foreach (Transform child in gridRoot) { Destroy(child.gameObject); }
                }
                grid.m_width = 0;
                grid.m_height = 0;

                if (!selectionWired) {
                    grid.m_onSelected += (g, item, pos, mod) => OnItemClicked(item, pos);
                    grid.m_onRightClick += (g, item, pos) => { };
                    selectionWired = true;
                }
                return true;
            }

            // Places each captured item in its own slot of a temp inventory. Items are added to the
            // backing list directly (not via AddItem) so distinct stacks are never merged and the
            // exact ItemData references are preserved for selection mapping.
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
                    if (selected.Count >= pickLimit) { return; } // at the pick cap
                    selected.Add(item);
                    SetHighlight(pos, true);
                }
                UpdateCounter();
            }

            // Toggles a translucent overlay on the slot at the given grid position. Elements are
            // created row-major under gridRoot, so index = y*width + x. The overlay is a non-raycast
            // child that UpdateGui never touches, so it survives the per-frame refresh.
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
                int randomFill = Mathf.Max(0, keepBudget - selected.Count);
                if (randomFill > 0) {
                    line += "  -  " + string.Format(Localization.instance.Localize("$dso_random_note"), randomFill);
                }
                counterText.text = line;
            }
        }
    }
}
