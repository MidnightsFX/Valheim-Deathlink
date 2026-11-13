using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Deathlink.Common.DataObjects;
using static Deathlink.Death.DeathChoices;

namespace Deathlink.Death
{
    public static class DeathChoiceEnable
    {
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class ShowDeathChoiceUI {
            public static void Postfix(InventoryGui __instance) {
                if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
                    if (Player.m_localPlayer != null && Player.m_localPlayer.TryGetUniqueKeyValue(DeathChoiceKey, out _)) { return; }
                } else {
                    if (Player.m_localPlayer != null && DeathConfigurationData.playerSettings.ContainsKey(Player.m_localPlayer.GetPlayerID())) { return; }
                }

                // Only the persistent singleton: a second copy on InventoryGui (rebuilt on world reload) desyncs the shown choice from the saved one.
                DeathChoiceUI.Instance.Show();
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        public static class HideDeathChoiceUI {
            public static void Postfix(InventoryGui __instance) {
                if (DeathChoiceUI.IsInitialized) { DeathChoiceUI.Instance.Hide(); }
            }
        }
    }

    public class DeathChoices
    {

        public class DeathChoiceUI : MonoBehaviour
        {
            public static DeathChoiceUI Instance {
                get {
                    if (_instance == null) {
                        GameObject holder = new GameObject("DeathlinkDeathChoiceUI");
                        DontDestroyOnLoad(holder);
                        _instance = holder.AddComponent<DeathChoiceUI>(); // AddComponent triggers Awake
                    }
                    return _instance;
                }
            }
            private static DeathChoiceUI _instance;
            public static bool IsInitialized => _instance != null;

            private static GameObject DeathChoicePanel;
            private static GameObject ChoicesContent;
            private static GameObject ChoicesContainer;
            private static TextMeshProUGUI DeathPenaltyDescription;
            private static Text CombatModifiersDescription;
            private static Text XPModifiersDescription;
            private static Text LootModifersDescription;
            private static Text HarvestModifiersDescription;

            private static ToggleGroup choiceGroup;
            private static string selectedDeathChoice = "none";
            private static bool isChangeMode = false;

            public void Awake()
            {
                _instance = this;
                CreateStaticUIObjects();
                SetChoiceList();
            }

            public void Show() {
                if (DeathChoicePanel == null) {
                    CreateStaticUIObjects();
                    SetChoiceList();
                }
                if (DeathChoicePanel == null) { return; }
                ClearSelection();
                DeathChoicePanel.SetActive(true);
            }

            // Otherwise Select would take whatever was highlighted the last time the panel was open.
            private static void ClearSelection() {
                selectedDeathChoice = "none";
                if (choiceGroup != null) { choiceGroup.SetAllTogglesOff(); }
            }

            // Selecting a choice from here uses up one of the player's allowed changes.
            public void ShowForChange() {
                isChangeMode = true;
                Show();
            }

            public void Hide() {
                if (DeathChoicePanel != null) {
                    DeathChoicePanel.SetActive(false);
                }
                isChangeMode = false;
            }

            // Called when the death levels reload. Skipped until the panel exists, since reading Instance would build it.
            internal static void RefreshIfBuilt() {
                if (IsInitialized == false || ChoicesContent == null) { return; }
                Instance.SetChoiceList();
            }

            public void MakePlayerDeathSelection() {
                if (Player.m_localPlayer == null) {
                    Logger.LogWarning("Player not set, ensure the local player is set.");
                    return;
                }
                if (!DeathConfigurationData.DeathLevels.ContainsKey(selectedDeathChoice)) {
                    Logger.LogWarning("No death type selected");
                    return;
                }
                Logger.LogDebug($"Player selected death type {selectedDeathChoice}");
                long playerID = Player.m_localPlayer.GetPlayerID();
                // Update the stored choice in place so an existing change counter is preserved.
                if (DeathConfigurationData.playerSettings.ContainsKey(playerID)) {
                    DeathConfigurationData.playerSettings[playerID].DeathChoiceLevel = selectedDeathChoice;
                } else {
                    DeathConfigurationData.playerSettings.Add(playerID, new DeathConfiguration() { DeathChoiceLevel = selectedDeathChoice });
                }
                Player.m_localPlayer.RemoveUniqueKeyValue(DeathChoiceKey);
                Player.m_localPlayer.AddUniqueKeyValue(DeathChoiceKey, selectedDeathChoice);
                if (isChangeMode) {
                    DeathConfigurationData.IncrementPlayerChangesUsed(Player.m_localPlayer);
                }
                DeathConfigurationData.CheckAndSetPlayerDeathConfig(Player.m_localPlayer);
                DeathConfigurationData.WritePlayerChoices();
                Hide();
            }

            private void SetChoiceList()
            {
                // Destroyed, not just forgotten: old rows left under the content stay in the shared ToggleGroup.
                foreach (Transform child in ChoicesContent.transform) {
                    GameObject.Destroy(child.gameObject);
                }
                selectedDeathChoice = "none";
                int y_value = -75;
                foreach (var entry in DeathConfigurationData.DeathLevels) {
                    var newDeathChoice = GameObject.Instantiate(ChoicesContainer, ChoicesContent.transform);
                    var selector = newDeathChoice.transform.Find("selecter");
                    selector.Find("Label").GetComponent<Text>().text = entry.Key;
                    newDeathChoice.transform.Find("ChoiceName").GetComponent<Text>().text = entry.Value.DisplayName;
                    var toggle = selector.GetComponent<Toggle>();
                    toggle.group = choiceGroup;
                    toggle.onValueChanged.AddListener((isOn) => {
                        // The group also fires this with isOn=false on the toggle it switches off.
                        if (!isOn) { return; }
                        DeathPenaltyDescription.text = entry.Value.GetDeathStyleDescription();
                        CombatModifiersDescription.text = entry.Value.GetDamageModifierDescription();
                        XPModifiersDescription.text = entry.Value.GetSkillModiferDescription();
                        LootModifersDescription.text = entry.Value.GetLootModifiersDescription();
                        HarvestModifiersDescription.text = entry.Value.GetResourceModiferDescription();
                        selectedDeathChoice = entry.Key;
                    });
                    newDeathChoice.SetActive(true);
                    newDeathChoice.transform.localPosition = new Vector3() { x = 5, y = y_value };
                    y_value -= 50;
                }
            }

            private void CreateStaticUIObjects()
            {
                // Unity's fake null lets this rebuild after a world reload destroys the panel, but never builds a second one.
                if (DeathChoicePanel != null) { return; }
                if (!GUIManager.CustomGUIFront) {
                    Logger.LogWarning("GUIManager not setup, skipping static object creation.");
                    return;
                }
                Logger.LogDebug("Creating static UI");
                DeathChoicePanel = GUIManager.Instance.CreateWoodpanel(
                    parent: GUIManager.CustomGUIFront.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, 0),
                    width: 800,
                    height: 960,
                    draggable: true);
                DeathChoicePanel.SetActive(false);

                var textHeader = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$selection_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(50f, 430f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 30,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 350f,
                    height: 40f,
                    addContentSizeFitter: false);
                textHeader.name = "DLHeader";

                GameObject descgo = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$selection_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0f, 380f),
                    font: GUIManager.Instance.AveriaSerif,
                    fontSize: 20,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 560f,
                    height: 60f,
                    addContentSizeFitter: false);
                var descgotext = descgo.GetComponent<Text>();
                descgotext.resizeTextForBestFit = true;
                descgotext.resizeTextMaxSize = 20;
                descgotext.alignment = TextAnchor.MiddleCenter;

                GameObject closeButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$close"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(360f, 430f),
                    width: 60f,
                    height: 60f);
                Button bclose = closeButton.GetComponent<Button>();
                bclose.interactable = true;
                bclose.onClick.AddListener(Hide);
                closeButton.SetActive(true);

                CreateSectionTitle("DeathPenaltyTitle", "$death_penalty_header", 330f);
                DeathPenaltyDescription = CreateTMPText(
                    name: "DeathPenaltyDesc",
                    text: Localization.instance.Localize("$death_penalty_description"),
                    parent: DeathChoicePanel.transform,
                    position: new Vector2(147f, 240f),
                    fontSize: 18,
                    width: 490f,
                    height: 160f);

                CreateSectionTitle("combatModifiersTitle", "$combat_header", 150f);
                CombatModifiersDescription = CreateSectionText("combatModifiersDesc", "$combat_modifier_description", 80f, 100f);

                CreateSectionTitle("xpModifiersTitle", "$xp_header", 30f);
                XPModifiersDescription = CreateSectionText("xpModifiersDesc", "$xp_mod_description", -80f, 200f);

                CreateSectionTitle("lootModifiersTitle", "$loot_header", -130f);
                LootModifersDescription = CreateSectionText("lootModifersDesc", "$loot_modifier_description", -240f, 200f);

                CreateSectionTitle("harvestModifiersTitle", "$harvest_header", -260f);
                HarvestModifiersDescription = CreateSectionText("harvestModifersDesc", "$harvest_modifier_description", -370f, 200f);

                GameObject selectButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$deathchoice_select"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-240f, -290f),
                    width: 200f,
                    height: 80f);
                selectButton.GetComponent<Button>().onClick.AddListener(MakePlayerDeathSelection);

                Logger.LogDebug("Setting up scroll entry");
                GameObject scrollView = GUIManager.Instance.CreateScrollView(DeathChoicePanel.transform, false, true, 10f, 10f, GUIManager.Instance.ValheimScrollbarHandleColorBlock, Color.grey, 250f, 400f);
                scrollView.transform.localPosition = new Vector2 { x = -260, y = -30 };
                ChoicesContent = scrollView.GetComponentInChildren<ContentSizeFitter>().gameObject;
                scrollView.GetComponentInChildren<ScrollRect>().scrollSensitivity = 200;
                choiceGroup = ChoicesContent.AddComponent<ToggleGroup>();

                Logger.LogDebug("Setting up death choice template entry");

                ChoicesContainer = new GameObject("DeathChoice");
                ChoicesContainer.transform.SetParent(DeathChoicePanel.transform, false);
                RectTransform rt = ChoicesContainer.AddComponent<RectTransform>();
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
                rt.position = new Vector3(0, 0);
                LayoutElement le = ChoicesContainer.AddComponent<LayoutElement>();
                le.minHeight = 50;
                ChoicesContainer.SetActive(false);

                var toggleGo = GUIManager.Instance.CreateToggle(
                    parent: ChoicesContainer.transform,
                    width: 40f,
                    height: 40f
                    );
                toggleGo.transform.localPosition = new Vector2(10f, 0f);
                toggleGo.name = "selecter";
                toggleGo.transform.Find("Label").gameObject.SetActive(false);
                toggleGo.GetComponent<Toggle>().isOn = false;

                var deathSettingName = GUIManager.Instance.CreateText(
                    text: "Name",
                    parent: ChoicesContainer.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(200f, 0f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 20,
                    color: GUIManager.Instance.ValheimYellow,
                    outline: true,
                    outlineColor: Color.black,
                    width: 350f,
                    height: 40f,
                    addContentSizeFitter: false);
                deathSettingName.name = "ChoiceName";
            }

            private static void CreateSectionTitle(string name, string locKey, float y) {
                GameObject title = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize(locKey),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, y),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                title.name = name;
            }

            private static Text CreateSectionText(string name, string locKey, float y, float height) {
                GameObject go = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize(locKey),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(150f, y),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 16,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 500f,
                    height: height,
                    addContentSizeFitter: false);
                go.name = name;
                Text text = go.GetComponent<Text>();
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = 18;
                return text;
            }

            // GUIManager.CreateText only makes legacy UI.Text, and the item-destruction skull glyph only
            // exists in Valheim's TMP fallback fonts.
            private static TextMeshProUGUI CreateTMPText(string name, string text, Transform parent, Vector2 position, float fontSize, float width, float height) {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = position;
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

                TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
                // The serif face the rest of the panel uses; Jotunn only exposes the sans one as a TMP font.
                TMP_FontAsset font = PrefabManager.Cache.GetPrefab<TMP_FontAsset>("Valheim-AveriaSerifLibre");
                tmp.font = font != null ? font : GUIManager.Instance.TMP_AveriaSansLibre;
                tmp.fontStyle = FontStyles.Bold;
                tmp.fontSize = fontSize;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 10f;
                tmp.fontSizeMax = fontSize;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.overflowMode = TextOverflowModes.Truncate;
                // Same values as Valheim's own "- Outline" material presets. Set on this instance rather
                // than loading a preset by name: two materials share that name, bound to different atlases.
                tmp.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.175f);
                tmp.outlineColor = Color.black;
                tmp.outlineWidth = 0.175f;
                tmp.text = text;
                return tmp;
            }
        }
    }
}
