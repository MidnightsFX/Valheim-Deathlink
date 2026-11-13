using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace Deathlink.Common {
#pragma warning restore IDE0130

    // A filterable "pick one of these" overlay.
    //
    // This is what stands in for a dropdown throughout the kit. Unity's Dropdown builds its option list as
    // a child of its own root, so putting one inside a scroll view means the viewport's Mask clips the
    // list and the popup is simply invisible. This parents to CustomGUIFront instead -- above everything,
    // clipped by nothing -- and a filter box makes it usable for the several-hundred-entry lists (prefab
    // names) that a dropdown could never handle anyway.
    internal static class ConfigUIPicker {
        private const float PanelW = 420f;
        private const float PanelH = 520f;
        private const float RowH = 30f;
        private const int MaxRows = 400;

        private static GameObject overlay;

        internal static bool IsOpen {
            get { return overlay != null; }
        }

        internal static void ShowPicker(string title, IList<string> options, string current, Action<string> onPick) {
            Close();
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null) { return; }

            List<string> all = new List<string>();
            if (options != null) {
                foreach (string option in options) {
                    if (string.IsNullOrEmpty(option) == false) { all.Add(option); }
                }
            }
            all.Sort(StringComparer.OrdinalIgnoreCase);

            // A full-screen transparent catcher behind the panel, so a click anywhere outside dismisses
            // the picker and, more importantly, so clicks cannot fall through onto the editor beneath.
            overlay = ConfigUI.NewUI("ConfigUIPickerOverlay", GUIManager.CustomGUIFront.transform, typeof(Image));
            RectTransform overlayRT = (RectTransform)overlay.transform;
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            Image catcher = overlay.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0.35f);
            Button catcherButton = overlay.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(Close);

            // Held for the picker's own lifetime. Jotunn counts blocks, so closing this does not unblock
            // input while the editor underneath is still open.
            overlay.AddComponent<ConfigUI.ConfigUIInputGuard>().Hold();

            GameObject panel = GUIManager.Instance.CreateWoodpanel(
                parent: overlay.transform,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0f, 0f), width: PanelW, height: PanelH, draggable: true);

            ConfigUI.AddText(panel.transform, 0f, 14f, PanelW, 30f, title, 18, TextAnchor.MiddleCenter,
                GUIManager.Instance.ValheimYellow);

            InputField filter = ConfigUI.AddTextField(panel.transform, 16f, 52f, PanelW - 100f, "", null,
                InputField.ContentType.Standard, ConfigUI.L("$menu_filter", "Filter") + "...");
            ConfigUI.AddButton(panel.transform, PanelW - 76f, 52f, 60f, ConfigUI.L("$menu_close", "Close"), Close, 28f);

            ConfigUI.CreateScroll(panel.transform, 16f, 92f, PanelW - 32f, PanelH - 116f,
                out Transform content, out float contentW);
            if (content == null) { return; }

            // Rows are built once and relabelled on each keystroke. Building a fresh Jotunn button for every
            // match on every keystroke hitched visibly against the full prefab table.
            List<GameObject> rows = new List<GameObject>();
            List<Text> labels = new List<Text>();
            List<string> rowOptions = new List<string>();
            GameObject statusRow = ConfigUI.NewLayoutRow(content, contentW, RowH);
            Text status = ConfigUI.AddText(statusRow.transform, 4f, 0f, contentW, RowH, "", 14, TextAnchor.MiddleLeft);

            Action<string> refresh = needle => {
                int matched = 0;
                foreach (string option in all) {
                    if (string.IsNullOrEmpty(needle) == false
                        && option.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                    matched++;
                    if (matched > MaxRows) { continue; }

                    int index = matched - 1;
                    if (index == rows.Count) {
                        GameObject row = ConfigUI.NewLayoutRow(content, contentW, RowH);
                        GameObject button = ConfigUI.AddButton(row.transform, 0f, 0f, contentW, "", () => {
                            string picked = rowOptions[index];
                            Close();
                            onPick?.Invoke(picked);
                        }, RowH - 2f);
                        rows.Add(row);
                        labels.Add(button.GetComponentInChildren<Text>());
                        rowOptions.Add(null);
                    }
                    rowOptions[index] = option;
                    labels[index].text = ConfigUI.L(option);
                    rows[index].SetActive(true);
                }

                int shown = Math.Min(matched, MaxRows);
                for (int i = shown; i < rows.Count; i++) { rows[i].SetActive(false); }

                statusRow.transform.SetAsLastSibling();
                statusRow.SetActive(matched == 0 || matched > shown);
                if (matched == 0) {
                    status.fontSize = 14;
                    status.text = ConfigUI.L("$modquickconfig_no_matches", "No matches");
                } else if (matched > shown) {
                    status.fontSize = 13;
                    string more = ConfigUI.L("$modquickconfig_more_matches", "... $1 more, type to narrow");
                    status.text = more.Replace("$1", (matched - shown).ToString());
                }
            };

            filter.onValueChanged.AddListener(needle => refresh(needle));
            refresh("");
        }

        internal static void Close() {
            if (overlay == null) { return; }
            UnityEngine.Object.Destroy(overlay);
            overlay = null;
        }
    }
}
