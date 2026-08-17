using Deathlink.Common;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    // The in-game death-choice editor, reached from the shared config launcher.
    //
    // Built on Common/Config/UI. Panel is destroyed and rebuilt on every open, and the detail pane is
    // rebuilt on every selection change -- that sidesteps two-way binding entirely, which is worth far
    // more than the widgets it costs to recreate.
    internal static class DeathChoiceEditor
    {
        private const string EntryName = "Deathlink";
        private const float PanelW = 1000f;
        private const float PanelH = 720f;
        private const float ListW = 250f;
        private const float DetailX = ListW + 30f;
        private const float DetailY = 96f;
        private const float LabelW = 230f;
        private const float MessagesH = 60f;

        private static GameObject panel;
        private static Transform body;
        private static GameObject detailRoot;
        private static Transform listContent;
        private static Text messages;
        private static DeathChoiceEditorModel model;
        private static int activeTab;
        private static bool awaitingServer;
        private static float sentAtTime;

        private static readonly string[] TabNames = { "$dl_cfg_general", "$dl_cfg_deathstyle", "$dl_cfg_resources", "$dl_cfg_loot" };

        internal static void Init() {
            ConfigUILauncher.Init();
            ApplyRegistration();
            ConfigNetwork.EditResult += OnEditResult;
        }

        internal static void ApplyRegistration() {
            if (ValConfig.ShowQuickConfigButton == null || ValConfig.ShowQuickConfigButton.Value) {
                ConfigUILauncher.Register(EntryName, OpenPanel);
            } else {
                ConfigUILauncher.Unregister(EntryName);
            }
        }

        // Whether this machine may write the file directly, as opposed to asking the server to.
        private static bool IsOwner() {
            return ZNet.instance == null || ZNet.instance.IsServer();
        }

        internal static void OpenPanel() {
            ClosePanel();
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null) { return; }

            model = DeathChoiceEditorModel.Snapshot();
            awaitingServer = false;
            activeTab = 0;

            panel = ConfigUI.CreatePanel("$dl_cfg_title", PanelW, PanelH, out body);

            ConfigUI.AddText(body, DetailX, 54f, PanelW - DetailX - 20f, 24f,
                IsOwner() ? "$dl_cfg_banner_local" : "$dl_cfg_banner_remote", 13, TextAnchor.MiddleLeft,
                GUIManager.Instance.ValheimBeige);

            BuildLevelList();
            BuildTabs();
            BuildDetail();

            messages = ConfigUI.AddText(body, 20f, PanelH - 112f, PanelW - 40f, MessagesH, "", 13,
                TextAnchor.UpperLeft);

            ConfigUI.AddButton(body, 20f, PanelH - 52f, 130f, "$dl_cfg_validate", RunValidate, 36f);
            ConfigUI.AddButton(body, PanelW - 330f, PanelH - 52f, 140f, "$dl_cfg_cancel", ClosePanel, 36f);
            ConfigUI.AddButton(body, PanelW - 180f, PanelH - 52f, 160f, "$dl_cfg_apply", ApplyAndSave, 36f);
        }

        private static void ClosePanel() {
            ConfigUIPicker.Close();
            if (panel != null) { UnityEngine.Object.Destroy(panel); panel = null; }
            body = null;
            detailRoot = null;
            listContent = null;
            messages = null;
            awaitingServer = false;
        }

        // --- Left hand list -------------------------------------------------------------------------

        private static void BuildLevelList() {
            ConfigUI.AddText(body, 20f, 54f, ListW, 24f, "$dl_cfg_levels", 15, TextAnchor.MiddleLeft,
                GUIManager.Instance.ValheimYellow);
            ConfigUI.CreateScroll(body, 20f, 82f, ListW, PanelH - 240f, out listContent, out float contentW);
            RefreshLevelList(contentW);

            float y = PanelH - 150f;
            ConfigUI.AddButton(body, 20f, y, 58f, "$dl_cfg_add", OnAdd, 30f);
            ConfigUI.AddButton(body, 82f, y, 58f, "$dl_cfg_duplicate", OnDuplicate, 30f);
            ConfigUI.AddButton(body, 144f, y, 58f, "$dl_cfg_rename", OnRename, 30f);
            ConfigUI.AddButton(body, 206f, y, 58f, "$dl_cfg_delete", OnDelete, 30f);
        }

        private static void RefreshLevelList(float contentW) {
            if (listContent == null) { return; }
            foreach (Transform child in listContent) { UnityEngine.Object.Destroy(child.gameObject); }

            foreach (string key in model.Order) {
                string entry = key;
                bool selected = string.Equals(entry, model.SelectedKey, StringComparison.Ordinal);
                GameObject row = ConfigUI.NewLayoutRow(listContent, contentW, 32f);
                GameObject button = ConfigUI.AddButton(row.transform, 0f, 0f, contentW, entry, () => {
                    model.SelectedKey = entry;
                    RefreshLevelList(contentW);
                    BuildDetail();
                }, 30f);
                if (selected) {
                    Text caption = button.GetComponentInChildren<Text>();
                    if (caption != null) { caption.color = GUIManager.Instance.ValheimOrange; }
                }
            }
        }

        private static void RebuildList() {
            // The scroll content width is fixed at build time; recompute it the same way CreateScroll did.
            RefreshLevelList(ListW - 16f);
            BuildDetail();
        }

        private static void OnAdd() {
            ConfigUIPrompt.Show("$dl_cfg_add_title", "$dl_cfg_add_label", "", null, name => {
                if (model.TryAdd(name, out string message) == false) { ShowError(message); return; }
                if (string.IsNullOrEmpty(message) == false) { ShowWarning(message); }
                RebuildList();
            });
        }

        private static void OnDuplicate() {
            if (string.IsNullOrEmpty(model.SelectedKey)) { return; }
            if (model.Duplicate(model.SelectedKey) == null) { return; }
            RebuildList();
        }

        private static void OnRename() {
            if (string.IsNullOrEmpty(model.SelectedKey)) { return; }
            string oldKey = model.SelectedKey;

            // The warning is fixed text, not a validation result: renaming is destructive in a way the
            // admin cannot see from the panel, because the consequence lands on other people's characters.
            string warning = ConfigUI.L("$dl_cfg_rename_warning").Replace("{0}", oldKey);
            if (ValConfig.DefaultDeathChoice != null
                && string.Equals(ValConfig.DefaultDeathChoice.Value, oldKey, StringComparison.Ordinal)) {
                warning += "\n" + ConfigUI.L("$dl_cfg_rename_isdefault");
            }

            ConfigUIPrompt.Show("$dl_cfg_rename_title", "$dl_cfg_rename_label", oldKey, warning, newKey => {
                if (model.TryRename(oldKey, newKey, out string message) == false) { ShowError(message); return; }
                if (string.IsNullOrEmpty(message) == false) { ShowWarning(message); }
                RebuildList();
            });
        }

        private static void OnDelete() {
            if (string.IsNullOrEmpty(model.SelectedKey)) { return; }
            if (model.TryDelete(model.SelectedKey, out string message) == false) { ShowError(message); return; }
            if (string.IsNullOrEmpty(message) == false) { ShowWarning(message); }
            RebuildList();
        }

        // --- Tabs and detail pane --------------------------------------------------------------------

        private static void BuildTabs() {
            float x = DetailX;
            for (int i = 0; i < TabNames.Length; i++) {
                int index = i;
                ConfigUI.AddButton(body, x, 78f, 170f, TabNames[index], () => {
                    activeTab = index;
                    BuildDetail();
                }, 30f);
                x += 176f;
            }
        }

        private static void BuildDetail() {
            if (detailRoot != null) { UnityEngine.Object.Destroy(detailRoot); detailRoot = null; }
            if (body == null) { return; }

            float width = PanelW - DetailX - 20f;
            float height = PanelH - DetailY - 130f;
            detailRoot = ConfigUI.NewRect("Detail", body, DetailX, DetailY + 20f, width, height);

            if (string.IsNullOrEmpty(model.SelectedKey)
                || model.Levels.TryGetValue(model.SelectedKey, out DeathChoiceLevel level) == false) {
                ConfigUI.AddText(detailRoot.transform, 0f, 0f, width, 30f, "$dl_cfg_noselection", 15,
                    TextAnchor.MiddleLeft);
                return;
            }

            switch (activeTab) {
                case 1: BuildDeathStyleTab(level, width, height); break;
                case 2: BuildResourcesTab(level, width, height); break;
                case 3: BuildLootTab(level, width, height); break;
                default: BuildGeneralTab(level, width); break;
            }
        }

        private static void BuildGeneralTab(DeathChoiceLevel level, float width) {
            List<GameObject> rows = new List<GameObject>();
            Transform parent = detailRoot.transform;

            rows.Add(ConfigUI.AddTextRow(parent, width, ConfigUI.SubRowHeight,
                ConfigUI.L("$dl_cfg_key") + ": " + model.SelectedKey, 14, GUIManager.Instance.ValheimOrange));
            rows.Add(ConfigUI.AddTextFieldRow(parent, width, LabelW, 300f, "$dl_cfg_displayname",
                level.DisplayName, s => level.DisplayName = s, null, 64));
            rows.Add(ConfigUI.AddToggleRow(parent, width, LabelW, "$dl_cfg_fallback", level.Fallback, on => {
                // Radio, not a plain bool: only one level can be the fallback.
                if (on) { model.SetFallback(model.SelectedKey); } else { level.Fallback = false; }
                BuildDetail();
            }));
            rows.Add(ConfigUI.AddSliderRow(parent, width, LabelW, 230f, 60f, "$dl_cfg_deathskillrate",
                0f, 10f, level.DeathSkillRate, false, v => level.DeathSkillRate = v));
            rows.Add(ConfigUI.AddSliderRow(parent, width, LabelW, 230f, 60f, "$dl_cfg_damagetaken",
                0f, 3f, level.DamageTakenModifier, false, v => level.DamageTakenModifier = v));
            rows.Add(ConfigUI.AddSliderRow(parent, width, LabelW, 230f, 60f, "$dl_cfg_damagedone",
                0f, 3f, level.DamageDoneModifier, false, v => level.DamageDoneModifier = v));

            ConfigUI.LayoutColumn(rows, 0f, 0f);
        }

        private static void BuildDeathStyleTab(DeathChoiceLevel level, float width, float height) {
            if (level.DeathStyle == null) { level.DeathStyle = new DeathProgressionDetails(); }
            DeathProgressionDetails style = level.DeathStyle;

            ConfigUI.CreateScroll(detailRoot.transform, 0f, 0f, width, height, out Transform content, out float w);
            if (content == null) { return; }

            AddScrollEnumCycle(content, w, "$dl_cfg_itemlossstyle", Enum.GetNames(typeof(ItemLossStyle)),
                (int)style.itemLossStyle, i => {
                    style.itemLossStyle = (ItemLossStyle)i;
                    // The keep budgets only mean anything for DeathlinkBased, so the rows come and go --
                    // rebuild rather than leave dead sliders on screen.
                    BuildDetail();
                });

            bool destroysEverything = style.itemLossStyle == ItemLossStyle.DestroyAll;
            if (destroysEverything == false) {
                AddScrollEnumCycle(content, w, "$dl_cfg_itemsavedstyle", Enum.GetNames(typeof(ItemSavedStyle)),
                    (int)style.itemSavedStyle, i => style.itemSavedStyle = (ItemSavedStyle)i);
                AddScrollEnumCycle(content, w, "$dl_cfg_nonskillchecked",
                    Enum.GetNames(typeof(NonSkillCheckedItemAction)), (int)style.nonSkillCheckedItemAction,
                    i => style.nonSkillCheckedItemAction = (NonSkillCheckedItemAction)i);
                AddScrollToggle(content, w, "$dl_cfg_enablesavingchoices", style.EnableItemSavingChoices,
                    v => style.EnableItemSavingChoices = v);
            }

            // Hidden rather than zeroed, so flipping back to DeathlinkBased restores what was configured.
            if (style.itemLossStyle == ItemLossStyle.DeathlinkBased) {
                AddScrollHeader(content, w, "$dl_cfg_keepbudget");
                AddScrollSlider(content, w, "$dl_cfg_minitems", 0f, 64f, style.minItemsKept, true, v => style.minItemsKept = (int)v);
                AddScrollSlider(content, w, "$dl_cfg_maxitems", 0f, 64f, style.maxItemsKept, true, v => style.maxItemsKept = (int)v);
                AddScrollSlider(content, w, "$dl_cfg_minequip", 0f, 16f, style.minEquipmentKept, true, v => style.minEquipmentKept = (int)v);
                AddScrollSlider(content, w, "$dl_cfg_maxequip", 0f, 16f, style.maxEquipmentKept, true, v => style.maxEquipmentKept = (int)v);
                AddScrollSlider(content, w, "$dl_cfg_minchoices", 0f, 16f, style.minItemsKeptChoices, true, v => style.minItemsKeptChoices = (int)v);
                AddScrollSlider(content, w, "$dl_cfg_maxchoices", 0f, 16f, style.maxItemsKeptChoices, true, v => style.maxItemsKeptChoices = (int)v);
            }

            AddScrollHeader(content, w, "$dl_cfg_skillloss");
            AddScrollToggle(content, w, "$dl_cfg_skilllossondeath", style.skillLossOnDeath, v => style.skillLossOnDeath = v);
            if (style.skillLossOnDeath) {
                // Labelled with the direction on purpose: Max applies at ZERO Deathlink skill, which is the
                // opposite of what the names suggest, and the validator only catches min > max.
                AddScrollSlider(content, w, "$dl_cfg_minskillloss", 0f, 1f, style.minSkillLossPercentage, false,
                    v => style.minSkillLossPercentage = v);
                AddScrollSlider(content, w, "$dl_cfg_maxskillloss", 0f, 1f, style.maxSkillLossPercentage, false,
                    v => style.maxSkillLossPercentage = v);
            }

            AddScrollHeader(content, w, "$dl_cfg_food");
            AddScrollToggle(content, w, "$dl_cfg_foodloss", style.foodLossOnDeath, v => {
                style.foodLossOnDeath = v;
                BuildDetail();
            });
            if (style.foodLossOnDeath) {
                AddScrollToggle(content, w, "$dl_cfg_fooddeathlink", style.foodLossUsesDeathlink,
                    v => style.foodLossUsesDeathlink = v);
            }
        }

        private static void BuildResourcesTab(DeathChoiceLevel level, float width, float height) {
            ConfigUI.CreateScroll(detailRoot.transform, 0f, 0f, width, height, out Transform content, out float w);
            if (content == null) { return; }

            if (level.ResourceModifiers == null) { level.ResourceModifiers = new Dictionary<string, DeathResourceModifier>(); }
            if (level.SkillModifiers == null) { level.SkillModifiers = new Dictionary<string, DeathSkillModifier>(); }

            AddScrollHeader(content, w, "$dl_cfg_resourcemods");
            foreach (KeyValuePair<string, DeathResourceModifier> pair in new List<KeyValuePair<string, DeathResourceModifier>>(level.ResourceModifiers)) {
                string entryKey = pair.Key;
                DeathResourceModifier modifier = pair.Value;
                if (modifier == null) { continue; }

                AddEntryHeader(content, w, entryKey, () => {
                    level.ResourceModifiers.Remove(entryKey);
                    BuildDetail();
                });
                AddScrollSlider(content, w, "$dl_cfg_bonusmodifier", 0f, 5f, modifier.BonusModifier, false,
                    v => modifier.BonusModifier = v);
                AddScrollToggle(content, w, "$dl_cfg_skillinfluence", modifier.skillInfluence,
                    v => modifier.skillInfluence = v);
                AddBonusActions(content, w, modifier.bonusActions);
                if (modifier.prefabs == null) { modifier.prefabs = new List<string>(); }
                ConfigUI.AddStringListEditor(content, w, "$dl_cfg_prefabs", modifier.prefabs,
                    BuildDetail, PrefabNames, IsKnownPrefab);
            }
            AddAddButton(content, w, "$dl_cfg_addresource", () => {
                level.ResourceModifiers[UniqueEntryKey(level.ResourceModifiers.Keys, "NewResource")] =
                    new DeathResourceModifier() { BonusModifier = 1f, prefabs = new List<string>() };
                BuildDetail();
            });

            AddScrollHeader(content, w, "$dl_cfg_skillmods");
            foreach (KeyValuePair<string, DeathSkillModifier> pair in new List<KeyValuePair<string, DeathSkillModifier>>(level.SkillModifiers)) {
                string entryKey = pair.Key;
                DeathSkillModifier modifier = pair.Value;
                if (modifier == null) { continue; }

                AddEntryHeader(content, w, entryKey, () => {
                    level.SkillModifiers.Remove(entryKey);
                    BuildDetail();
                });
                // A picker, not a cycle button: Skills.SkillType has around forty members.
                AddScrollPicker(content, w, "$dl_cfg_skill", modifier.skill.ToString(), SkillNames, s => {
                    if (Enum.IsDefined(typeof(Skills.SkillType), s)) {
                        modifier.skill = (Skills.SkillType)Enum.Parse(typeof(Skills.SkillType), s, true);
                    }
                }, IsKnownSkill);
                AddScrollSlider(content, w, "$dl_cfg_bonusmodifier", 0f, 5f, modifier.BonusModifier, false,
                    v => modifier.BonusModifier = v);
                AddScrollToggle(content, w, "$dl_cfg_skillinfluence", modifier.skillInfluence,
                    v => modifier.skillInfluence = v);
            }
            AddAddButton(content, w, "$dl_cfg_addskill", () => {
                level.SkillModifiers[UniqueEntryKey(level.SkillModifiers.Keys, "NewSkill")] =
                    new DeathSkillModifier() { BonusModifier = 1f, skill = Skills.SkillType.All };
                BuildDetail();
            });
        }

        private static void BuildLootTab(DeathChoiceLevel level, float width, float height) {
            ConfigUI.CreateScroll(detailRoot.transform, 0f, 0f, width, height, out Transform content, out float w);
            if (content == null) { return; }

            if (level.DeathLootModifiers == null) { level.DeathLootModifiers = new Dictionary<string, DeathLootModifier>(); }

            AddScrollHeader(content, w, "$dl_cfg_lootmods");
            foreach (KeyValuePair<string, DeathLootModifier> pair in new List<KeyValuePair<string, DeathLootModifier>>(level.DeathLootModifiers)) {
                string entryKey = pair.Key;
                DeathLootModifier modifier = pair.Value;
                if (modifier == null) { continue; }

                AddEntryHeader(content, w, entryKey, () => {
                    level.DeathLootModifiers.Remove(entryKey);
                    BuildDetail();
                });
                AddScrollPicker(content, w, "$dl_cfg_prefab", modifier.prefab, PrefabNames,
                    s => modifier.prefab = s, IsKnownPrefab);
                AddScrollSlider(content, w, "$dl_cfg_chance", 0f, 1f, modifier.chance, false, v => modifier.chance = v);
                AddScrollSlider(content, w, "$dl_cfg_amount", 1f, 100f, modifier.amount, true, v => modifier.amount = (int)v);
                AddBonusActions(content, w, modifier.bonusActions);
            }
            AddAddButton(content, w, "$dl_cfg_addloot", () => {
                level.DeathLootModifiers[UniqueEntryKey(level.DeathLootModifiers.Keys, "NewLoot")] =
                    new DeathLootModifier() { chance = 0.05f, amount = 1, bonusActions = new List<ResourceGainTypes>() };
                BuildDetail();
            });
        }

        // --- Scroll-content row helpers ----------------------------------------------------------------
        //
        // Rows inside a scroll view size themselves through a LayoutElement (NewLayoutRow), so these wrap
        // the kit's absolutely-positioned Add*Row helpers by giving each one its own layout row to sit in.

        private static void AddScrollHeader(Transform content, float w, string label) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 0f, 0f, w, ConfigUI.RowHeight, label, 16, TextAnchor.MiddleLeft,
                GUIManager.Instance.ValheimYellow);
        }

        private static void AddEntryHeader(Transform content, float w, string entryKey, Action onDelete) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, w - 110f, ConfigUI.RowHeight, entryKey, 15,
                TextAnchor.MiddleLeft, GUIManager.Instance.ValheimOrange);
            ConfigUI.AddButton(row.transform, w - 96f, 2f, 92f, "$dl_cfg_deleteentry", () => onDelete(), 28f);
        }

        private static void AddAddButton(Transform content, float w, string label, Action onAdd) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddButton(row.transform, 8f, 2f, 220f, label, () => onAdd(), 28f);
        }

        private static void AddScrollToggle(Transform content, float w, string label, bool value, Action<bool> onChange) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, LabelW, ConfigUI.RowHeight, label, 15, TextAnchor.MiddleLeft);
            ConfigUI.AddToggle(row.transform, LabelW + 14f, 4f, 24f, value, onChange);
        }

        private static void AddScrollSlider(Transform content, float w, string label, float min, float max,
            float value, bool whole, Action<float> onChange) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, LabelW, ConfigUI.RowHeight, label, 15, TextAnchor.MiddleLeft);
            Slider slider = ConfigUI.BuildSlider(row.transform, LabelW + 14f, 7f, 220f, min, max, value, whole);

            float boxX = LabelW + 244f;
            InputField box = ConfigUI.AddTextField(row.transform, boxX, 3f, 66f, ConfigUI.Fmt(value, whole), null,
                whole ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber);
            slider.onValueChanged.AddListener(v => {
                if (whole) { v = Mathf.Round(v); }
                box.SetTextWithoutNotify(ConfigUI.Fmt(v, whole));
                onChange(v);
            });
            box.onEndEdit.AddListener(str => {
                if (float.TryParse(str, out float v) == false) { v = slider.value; }
                v = Mathf.Clamp(v, min, max);
                if (whole) { v = Mathf.Round(v); }
                box.SetTextWithoutNotify(ConfigUI.Fmt(v, whole));
                if (slider.value != v) { slider.value = v; } else { onChange(v); }
            });
        }

        private static void AddScrollEnumCycle(Transform content, float w, string label, string[] options,
            int current, Action<int> onChange) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, LabelW, ConfigUI.RowHeight, label, 15, TextAnchor.MiddleLeft);

            int idx = Mathf.Clamp(current, 0, Math.Max(0, options.Length - 1));
            GameObject go = ConfigUI.AddButton(row.transform, LabelW + 14f, 2f, 210f,
                options.Length > 0 ? options[idx] : "", null, 28f);
            Text caption = go.GetComponentInChildren<Text>();
            go.GetComponent<Button>().onClick.AddListener(() => {
                if (options.Length == 0) { return; }
                idx = (idx + 1) % options.Length;
                caption.text = options[idx];
                onChange(idx);
            });
        }

        private static void AddScrollPicker(Transform content, float w, string label, string current,
            Func<IList<string>> options, Action<string> onPick, Func<string, bool> isKnown) {
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, LabelW, ConfigUI.RowHeight, label, 15, TextAnchor.MiddleLeft);

            InputField field = ConfigUI.AddTextField(row.transform, LabelW + 14f, 3f, 220f, current, onPick);
            Text marker = ConfigUI.AddText(row.transform, LabelW + 278f, 0f, 20f, ConfigUI.RowHeight, "", 15,
                TextAnchor.MiddleLeft, new Color(0.98f, 0.75f, 0.14f));
            Action refresh = () => {
                bool unknown = string.IsNullOrEmpty(field.text) == false && isKnown(field.text) == false;
                marker.text = unknown ? "!" : "";
            };
            refresh();
            field.onEndEdit.AddListener(_ => refresh());

            ConfigUI.AddButton(row.transform, LabelW + 240f, 3f, 34f, "...", () => {
                ConfigUIPicker.ShowPicker(label, options(), field.text, picked => {
                    field.SetTextWithoutNotify(picked);
                    refresh();
                    onPick(picked);
                });
            }, 28f);
        }

        private static void AddBonusActions(Transform content, float w, List<ResourceGainTypes> actions) {
            if (actions == null) { return; }
            GameObject row = ConfigUI.NewLayoutRow(content, w, ConfigUI.RowHeight);
            ConfigUI.AddText(row.transform, 8f, 0f, LabelW, ConfigUI.RowHeight, "$dl_cfg_bonusactions", 15,
                TextAnchor.MiddleLeft);

            string[] names = Enum.GetNames(typeof(ResourceGainTypes));
            float x = LabelW + 14f;
            for (int i = 0; i < names.Length; i++) {
                ResourceGainTypes value = (ResourceGainTypes)Enum.Parse(typeof(ResourceGainTypes), names[i]);
                ConfigUI.AddToggle(row.transform, x, 4f, 22f, actions.Contains(value), on => {
                    if (on) {
                        if (actions.Contains(value) == false) { actions.Add(value); }
                    } else {
                        actions.Remove(value);
                    }
                });
                ConfigUI.AddText(row.transform, x + 26f, 0f, 100f, ConfigUI.RowHeight, names[i], 13,
                    TextAnchor.MiddleLeft);
                x += 130f;
            }
        }

        // --- Apply ---------------------------------------------------------------------------------

        private static void RunValidate() {
            if (model == null) { return; }
            ValidationReport report = model.Validate();
            if (report.Errors.Count == 0 && report.Warnings.Count == 0) {
                messages.text = ConfigUI.L("$dl_cfg_looksgood");
                return;
            }
            ConfigUI.SetMessages(messages, report.Errors, report.Warnings);
        }

        private static void ApplyAndSave() {
            if (model == null || awaitingServer) { return; }

            try {
                ValidationReport report = model.Validate();
                if (report.HasErrors) {
                    // Panel stays open and nothing is written. Closing over a rejected save is how an admin
                    // ends up believing a change landed when it did not.
                    ConfigUI.SetMessages(messages, report.Errors, report.Warnings);
                    return;
                }

                YamlConfigFile<Dictionary<string, DeathChoiceLevel>> file = YamlConfigManager.DeathChoices;
                if (file != null && file.LastLoadedUtc != model.BaseLoadedUtc) {
                    // Somebody else changed the file while this panel was open. Say so once, then let a
                    // second press go through -- BaseLoadedUtc is advanced so the warning does not repeat.
                    model.BaseLoadedUtc = file.LastLoadedUtc;
                    ShowWarning(ConfigUI.L("$dl_cfg_changedunderneath"));
                    return;
                }

                string yaml = model.Serialize();

                if (IsOwner()) {
                    if (YamlConfigManager.ApplyEdited(file, yaml, out string message) == false) {
                        ShowError(message);
                        return;
                    }
                    if (string.IsNullOrEmpty(message) == false) { Logger.LogWarning(message); }
                    ClosePanel();
                    return;
                }

                if (ConfigNetwork.RequestEdit(file, yaml, out string refusal) == false) {
                    ShowError(refusal);
                    return;
                }
                awaitingServer = true;
                sentAtTime = Time.realtimeSinceStartup;
                messages.text = ConfigUI.L("$dl_cfg_sent");
                BepInEx.ThreadingHelper.Instance?.StartCoroutine(AwaitServerReply());
            } catch (Exception e) {
                // ClosePanel deliberately does NOT run here: a half-applied save must leave the panel up
                // with the staged edit intact so the admin can see what happened and retry.
                Logger.LogError($"Death choice editor failed to apply: {e}");
                ShowError(e.Message);
            }
        }

        private static System.Collections.IEnumerator AwaitServerReply() {
            while (awaitingServer && Time.realtimeSinceStartup - sentAtTime < 10f) {
                yield return null;
            }
            if (awaitingServer) {
                awaitingServer = false;
                ShowError(ConfigUI.L("$dl_cfg_noreply"));
            }
        }

        private static void OnEditResult(YamlConfigFile file, bool accepted, string message) {
            if (awaitingServer == false || file != YamlConfigManager.DeathChoices) { return; }
            awaitingServer = false;

            if (accepted == false) {
                ShowError(message);
                return;
            }
            if (string.IsNullOrEmpty(message) == false) { Logger.LogWarning(message); }
            ClosePanel();
        }

        private static void ShowError(string message) {
            ConfigUI.SetMessages(messages, new List<string>() { message }, null);
        }

        private static void ShowWarning(string message) {
            ConfigUI.SetMessages(messages, null, new List<string>() { message });
        }

        // --- Option sources ---------------------------------------------------------------------------

        private static IList<string> PrefabNames() {
            List<string> names = new List<string>();
            if (ZNetScene.instance == null) { return names; }
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs) {
                if (prefab != null) { names.Add(prefab.name); }
            }
            return names;
        }

        // Everything counts as known until the prefab table exists, so opening the editor from the main
        // menu does not paint every prefab field with a warning marker. Gated on Utils.PrefabsAvailable
        // rather than PrefabManager.Instance, which is never null -- see that helper.
        private static bool IsKnownPrefab(string name) {
            if (Common.Utils.PrefabsAvailable() == false) { return true; }
            return PrefabManager.Instance.GetPrefab(name) != null;
        }

        private static IList<string> SkillNames() {
            List<string> names = new List<string>() { Skills.SkillType.All.ToString() };
            foreach (string name in Enum.GetNames(typeof(Skills.SkillType))) {
                if (name == Skills.SkillType.All.ToString()) { continue; }
                names.Add(name);
            }
            return names;
        }

        private static bool IsKnownSkill(string name) {
            return Enum.IsDefined(typeof(Skills.SkillType), name);
        }

        private static string UniqueEntryKey(ICollection<string> existing, string candidate) {
            if (existing.Contains(candidate) == false) { return candidate; }
            for (int i = 2; i < 1000; i++) {
                if (existing.Contains(candidate + i) == false) { return candidate + i; }
            }
            return candidate + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}
