using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    /// <summary>
    /// Custom overlay panel showing the server leaderboard with three switchable views
    /// (Survival / Combat / Gathering). Shown while the "Leaderboard" page is selected in the
    /// Trophies/Compendium TextsDialog. Built with Jotunn's GUIManager, mirroring DeathChoiceUI.
    /// </summary>
    public static class LeaderboardUI
    {
        private const int ViewSurvival = 0;
        private const int ViewCombat = 1;
        private const int ViewGathering = 2;

        private static GameObject panel;
        private static GameObject content;            // scroll content the rows live under
        private static readonly List<GameObject> rows = new List<GameObject>();
        private static readonly Text[] headerCols = new Text[5];
        private static int currentView = ViewSurvival;

        private const float RowWidth = 820f;
        private const float RowHeight = 30f;

        // Per-column layout: x is the cell centre relative to the row centre, w is the cell width.
        private static readonly (float x, float w)[] columns = new (float x, float w)[] {
            (-310f, 200f), // player name
            (-135f, 150f), // death choice
            (  25f, 150f), // stat 1
            ( 185f, 150f), // stat 2
            ( 340f, 140f), // stat 3
        };

        // -----------------------------------------------------------------
        // Public control surface (called from the TextsDialog patches below)
        // -----------------------------------------------------------------

        public static bool IsVisible => panel != null && panel.activeSelf;

        public static void Show()
        {
            if (!EnsurePanel()) { return; }
            panel.SetActive(true);
            // Show the currently cached board right away, then push our latest stats and pull the
            // server's current state; the reply triggers RefreshIfVisible to repopulate with fresh data.
            PopulateView(currentView);
            LeaderboardData.RequestDashboardSync();
        }

        public static void Hide()
        {
            if (panel != null) { panel.SetActive(false); }
        }

        public static void RefreshIfVisible()
        {
            if (IsVisible) { PopulateView(currentView); }
        }

        // -----------------------------------------------------------------
        // Panel construction
        // -----------------------------------------------------------------

        private static bool EnsurePanel()
        {
            if (panel != null) { return true; }
            if (GUIManager.Instance == null || !GUIManager.CustomGUIFront) {
                Logger.LogWarning("GUIManager not setup, skipping leaderboard panel creation.");
                return false;
            }

            panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, 0),
                width: 900,
                height: 680,
                draggable: true);
            panel.SetActive(false);

            GUIManager.Instance.CreateText(
                text: Localization.instance.Localize("$leaderboard"),
                parent: panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 290f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 30,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 500f,
                height: 40f,
                addContentSizeFitter: false);

            var closeButton = GUIManager.Instance.CreateButton(
                text: Localization.instance.Localize("$close"),
                parent: panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(420f, 300f),
                width: 50f,
                height: 50f);
            closeButton.GetComponent<Button>().onClick.AddListener(Hide);

            CreateViewButton("$leaderboard_survival", -230f, ViewSurvival);
            CreateViewButton("$leaderboard_combat", 0f, ViewCombat);
            CreateViewButton("$leaderboard_gathering", 230f, ViewGathering);

            // Sticky column-header row above the scroll list.
            for (int i = 0; i < headerCols.Length; i++) {
                headerCols[i] = CreateText(panel, new Vector2(columns[i].x, 200f), columns[i].w,
                    "", GUIManager.Instance.ValheimOrange, 18);
            }

            var scrollView = GUIManager.Instance.CreateScrollView(
                panel.transform, false, true, 10f, 10f,
                GUIManager.Instance.ValheimScrollbarHandleColorBlock, Color.grey, 860f, 440f);
            scrollView.transform.localPosition = new Vector2(0f, -70f);
            scrollView.GetComponentInChildren<ScrollRect>().scrollSensitivity = 200;

            content = scrollView.GetComponentInChildren<ContentSizeFitter>().gameObject;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) { vlg = content.AddComponent<VerticalLayoutGroup>(); }
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;

            return true;
        }

        private static void CreateViewButton(string locKey, float x, int view)
        {
            var button = GUIManager.Instance.CreateButton(
                text: Localization.instance.Localize(locKey),
                parent: panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(x, 240f),
                width: 200f,
                height: 44f);
            button.GetComponent<Button>().onClick.AddListener(() => PopulateView(view));
        }

        // -----------------------------------------------------------------
        // Population
        // -----------------------------------------------------------------

        public static void PopulateView(int view)
        {
            currentView = view;
            if (!EnsurePanel()) { return; }

            foreach (var r in rows) { if (r != null) { UnityEngine.Object.Destroy(r); } }
            rows.Clear();

            SetHeaderLabels(view);

            IEnumerable<LeaderboardEntry> ordered;
            switch (view) {
                case ViewCombat:
                    ordered = LeaderboardData.leaderboard.Values
                        .OrderByDescending(e => e.BossKills).ThenByDescending(e => e.TotalDamage);
                    break;
                case ViewGathering:
                    ordered = LeaderboardData.leaderboard.Values
                        .OrderByDescending(e => e.TreeChops + e.Mines + e.CraftsAndBuilds);
                    break;
                default:
                    ordered = LeaderboardData.leaderboard.Values
                        .OrderByDescending(e => e.LongestLifeSeconds);
                    break;
            }

            foreach (var entry in ordered) {
                rows.Add(CreateRow(entry, view));
            }
        }

        private static void SetHeaderLabels(int view)
        {
            string[] keys;
            switch (view) {
                case ViewCombat:
                    keys = new[] { "$lb_col_player", "$lb_col_choice", "$lb_damage", "$lb_bosses", "" };
                    break;
                case ViewGathering:
                    keys = new[] { "$lb_col_player", "$lb_col_choice", "$lb_trees", "$lb_mining", "$lb_crafts" };
                    break;
                default:
                    keys = new[] { "$lb_col_player", "$lb_col_choice", "$lb_first", "$lb_longest", "$lb_average" };
                    break;
            }
            for (int i = 0; i < headerCols.Length; i++) {
                headerCols[i].text = string.IsNullOrEmpty(keys[i]) ? "" : Localization.instance.Localize(keys[i]);
            }
        }

        private static GameObject CreateRow(LeaderboardEntry entry, int view)
        {
            var row = new GameObject("LBRow", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(content.transform, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(RowWidth, RowHeight);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            le.minHeight = RowHeight;
            le.preferredWidth = RowWidth;

            CreateText(row, new Vector2(columns[0].x, 0f), columns[0].w, entry.PlayerName ?? "", GUIManager.Instance.ValheimYellow, 16);
            CreateText(row, new Vector2(columns[1].x, 0f), columns[1].w, entry.DeathChoice ?? "", Color.white, 16);

            switch (view) {
                case ViewCombat:
                    CreateText(row, new Vector2(columns[2].x, 0f), columns[2].w, Mathf.RoundToInt(entry.TotalDamage).ToString("N0"), Color.white, 16);
                    CreateText(row, new Vector2(columns[3].x, 0f), columns[3].w, entry.BossKills.ToString(), Color.white, 16);
                    break;
                case ViewGathering:
                    CreateText(row, new Vector2(columns[2].x, 0f), columns[2].w, entry.TreeChops.ToString(), Color.white, 16);
                    CreateText(row, new Vector2(columns[3].x, 0f), columns[3].w, entry.Mines.ToString(), Color.white, 16);
                    CreateText(row, new Vector2(columns[4].x, 0f), columns[4].w, entry.CraftsAndBuilds.ToString(), Color.white, 16);
                    break;
                default:
                    CreateText(row, new Vector2(columns[2].x, 0f), columns[2].w, FormatMinutes(entry.FirstLifeSeconds), Color.white, 16);
                    CreateText(row, new Vector2(columns[3].x, 0f), columns[3].w, FormatMinutes(entry.LongestLifeSeconds), Color.white, 16);
                    CreateText(row, new Vector2(columns[4].x, 0f), columns[4].w, FormatMinutes(entry.AverageLifeSeconds), Color.white, 16);
                    break;
            }
            return row;
        }

        private static Text CreateText(GameObject parent, Vector2 position, float width, string value, Color color, int fontSize)
        {
            var go = GUIManager.Instance.CreateText(
                text: value,
                parent: parent.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: position,
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: fontSize,
                color: color,
                outline: true,
                outlineColor: Color.black,
                width: width,
                height: RowHeight,
                addContentSizeFitter: false);
            var text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static string FormatMinutes(float seconds)
        {
            return $"{Mathf.RoundToInt(seconds / 60f)}m";
        }

        // -----------------------------------------------------------------
        // Trophies/Compendium (TextsDialog) integration
        // -----------------------------------------------------------------

        private static GameObject leaderboardButton;
        private static string DeathlinkSettingsTopic => Localization.instance.Localize("$deathlink_settings");

        // Show a "Leaderboard" button on the Deathlink settings page, to the left of the close box
        // (mirroring the "Change Death Choice" button that sits on the right). The overlay opens only
        // when this button is clicked, never automatically, so there is no separate leaderboard page
        // in the compendium list any more.
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new Type[] { typeof(TextsDialog.TextInfo) })]
        public static class TextsDialog_ShowText_Leaderboard_Patch
        {
            public static void Postfix(TextsDialog __instance, TextsDialog.TextInfo text)
            {
                bool onSettingsPage = text != null && text.m_topic == DeathlinkSettingsTopic;
                if (!ValConfig.EnableLeaderboard.Value || !onSettingsPage) {
                    if (leaderboardButton != null) { leaderboardButton.SetActive(false); }
                    return;
                }

                EnsureLeaderboardButton(__instance);
                if (leaderboardButton != null) {
                    leaderboardButton.SetActive(true);
                    leaderboardButton.transform.SetAsLastSibling();
                }
            }

            private static void EnsureLeaderboardButton(TextsDialog dialog)
            {
                if (leaderboardButton != null) { return; }
                if (GUIManager.Instance == null) {
                    Logger.LogWarning("GUIManager not setup, skipping leaderboard button creation.");
                    return;
                }

                Transform closebuttonTF = dialog.transform.Find("Texts_frame/Closebutton");
                if (closebuttonTF == null) { return; }

                leaderboardButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$leaderboard"),
                    parent: closebuttonTF,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-250f, 0f),
                    width: 200f,
                    height: 40f);
                leaderboardButton.GetComponent<Button>().onClick.AddListener(Show);
            }
        }

        // Ensure the overlay is hidden when the dialog (or inventory) closes.
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.OnClose))]
        public static class TextsDialog_OnClose_Leaderboard_Patch
        {
            public static void Postfix() { Hide(); }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        public static class InventoryGui_Hide_Leaderboard_Patch
        {
            public static void Postfix() { Hide(); }
        }
    }
}
