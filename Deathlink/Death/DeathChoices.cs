using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
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
                    if (Player.m_localPlayer != null && Player.m_localPlayer.PlayerHasUniqueKey(DeathChoiceKey)) { return; }
                } else {
                    if (Player.m_localPlayer != null && DeathConfigurationData.playerSettings.ContainsKey(Player.m_localPlayer.GetPlayerID())) { return; }
                }
                
                // Route through the persistent singleton holder only. Attaching the component to the
                // InventoryGui GameObject (which is torn down and recreated on world reload) produced a
                // second DeathChoiceUI instance and a second ToggleGroup over the same static state,
                // which is what desynced the highlighted choice from the saved one.
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
            private static GameObject ChoicesScrollView;
            private static GameObject ChoicesContent;
            private static GameObject ChoicesContainer;
            private static GameObject manualCloseButton;
            private static GameObject selectChoiceButton;
            private static Text DeathPenaltyDescription;
            private static Text CombatModifiersDescription;
            private static Text XPModifiersDescription;
            private static Text LootModifersDescription;
            private static Text HarvestModifiersDescription;

            private static List<Toggle> difficultyToggles = new List<Toggle>();
            private static ToggleGroup choiceGroup;
            private static string selectedDeathChoice = "none";
            // True when the panel was opened from the compendium "change my choice" button,
            // which consumes one of the player's allowed changes on selection.
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
                DeathChoicePanel.SetActive(true);
            }

            // Opens the selection panel as a one-time change initiated by the player. Selecting a
            // choice from here consumes one of the player's allowed changes.
            public void ShowForChange() {
                isChangeMode = true;
                Show();
            }

            public void Hide() {
                // Logger.LogDebug("Closing");
                if (DeathChoicePanel != null) {
                    DeathChoicePanel.SetActive(false);
                }
                isChangeMode = false;
                GUIManager.BlockInput(false);
            }

            public void MakePlayerDeathSelection() {
                if (Player.m_localPlayer == null) {
                    Logger.LogWarning("Player not set, ensure the local player is set.");
                    return;
                }
                if (selectedDeathChoice == "none") {
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
                // Replace any existing key so a changed choice overwrites the previous value.
                Player.m_localPlayer.PlayerRemoveUniqueKey(DeathChoiceKey);
                Player.m_localPlayer.AddUniqueKeyValue(DeathChoiceKey, selectedDeathChoice);
                if (isChangeMode) {
                    DeathConfigurationData.IncrementPlayerChangesUsed(Player.m_localPlayer);
                }
                DeathConfigurationData.CheckAndSetPlayerDeathConfig(Player.m_localPlayer);
                DeathConfigurationData.WritePlayerChoices();
                Hide();
            }

            // TODO make this configurable and loaded from a config file
            private void SetChoiceList()
            {
                // Destroy any rows from a previous build before repopulating. Clearing only the
                // tracking list (below) used to leave the old toggle GameObjects parented under the
                // content, so a rebuild stacked duplicate toggles into the shared ToggleGroup and the
                // saved choice could come from a different toggle than the one shown selected.
                foreach (Transform child in ChoicesContent.transform) {
                    GameObject.Destroy(child.gameObject);
                }
                difficultyToggles.Clear();
                int y_value = -75;
                //Logger.LogDebug($"Setting up {DeathChoices.Count} death styles.");
                foreach (var entry in DeathConfigurationData.DeathLevels) {
                    var newDeathChoice = GameObject.Instantiate(ChoicesContainer, ChoicesContent.transform);
                    //Logger.LogDebug("Created container");
                    var selector = newDeathChoice.transform.Find("selecter");
                    //Logger.LogDebug($"Finding selector... null? {selector == null}");
                    selector.Find("Label").GetComponent<Text>().text = entry.Key;
                    //Logger.LogDebug("Set label text");
                    newDeathChoice.transform.Find("ChoiceName").GetComponent<Text>().text = entry.Value.DisplayName;
                    //Logger.LogDebug("Set display name");
                    var toggle = selector.GetComponent<Toggle>();
                    toggle.group = choiceGroup;
                    toggle.onValueChanged.AddListener((isOn) => {
                        // Only react to the toggle being switched ON. The group also fires this
                        // callback with isOn=false on the previously-selected toggle when it is
                        // switched off, which would otherwise overwrite the descriptions/selection.
                        if (!isOn) { return; }
                        //Logger.LogDebug("Setting up onclock");
                        DeathPenaltyDescription.GetComponent<Text>().text = entry.Value.GetDeathStyleDescription();
                        //Logger.LogDebug("Set death description");
                        CombatModifiersDescription.GetComponent<Text>().text = entry.Value.GetDamageModifierDescription();
                        //Logger.LogDebug("Set combat mod");
                        XPModifiersDescription.GetComponent<Text>().text = entry.Value.GetSkillModiferDescription();
                        //Logger.LogDebug("Set xp mod");
                        LootModifersDescription.GetComponent<Text>().text = entry.Value.GetLootModifiersDescription();
                        //Logger.LogDebug("Set loot mod");
                        HarvestModifiersDescription.GetComponent<Text>().text = entry.Value.GetResourceModiferDescription();
                        //Logger.LogDebug("Set harvest mod");
                        selectedDeathChoice = entry.Key;
                    });
                    //Logger.LogDebug("Created onclick");
                    newDeathChoice.SetActive(true);
                    newDeathChoice.transform.localPosition = new Vector3() { x = 5, y = y_value };
                    difficultyToggles.Add(toggle);
                    y_value -= 50;
                }
            }

            private void CreateStaticUIObjects()
            {
                // Idempotent: never build a second panel/ToggleGroup over the shared static state.
                // Unity's fake-null makes this correctly rebuild after a real teardown (e.g. world
                // reload destroys the panel, so DeathChoicePanel == null again) while blocking a
                // duplicate build during a live session.
                if (DeathChoicePanel != null) { return; }
                if (GUIManager.Instance == null || !GUIManager.CustomGUIFront) {
                    Logger.LogWarning("GUIManager not setup, skipping static object creation.");
                    return;
                }
                Logger.LogDebug("Creating static UI");
                // Create the panel object
                DeathChoicePanel = GUIManager.Instance.CreateWoodpanel(
                    parent: GUIManager.CustomGUIFront.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(0, 0),
                    width: 800,
                    height: 960,
                    draggable: true);
                // Hide it right away
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

                manualCloseButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$close"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(360f, 430f),
                    width: 60f,
                    height: 60f);
                Button bclose = manualCloseButton.GetComponent<Button>();
                bclose.interactable = true;
                bclose.onClick.AddListener(Hide);
                manualCloseButton.SetActive(true);

                var deathpenaltyTitle = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$death_penalty_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, 330f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                deathpenaltyTitle.name = "DeathPenaltyTitle";

                var deathpenalty = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$death_penalty_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(147f, 240f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 18,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 490f,
                    height: 160f,
                    addContentSizeFitter: false);
                deathpenalty.name = "DeathPenaltyDesc";
                DeathPenaltyDescription = deathpenalty.GetComponent<Text>();
                DeathPenaltyDescription.resizeTextForBestFit = true;
                DeathPenaltyDescription.resizeTextMaxSize = 18;
                //DeathPenaltyDescription.verticalOverflow = VerticalWrapMode.Overflow;

                var combatTitle = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$combat_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, 150f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                combatTitle.name = "combatModifiersTitle";

                var combatMod = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$combat_modifier_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(150f, 80f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 16,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 500f,
                    height: 100f,
                    addContentSizeFitter: false);
                combatMod.name = "combatModifiersDesc";
                CombatModifiersDescription = combatMod.GetComponent<Text>();
                CombatModifiersDescription.resizeTextForBestFit = true;
                CombatModifiersDescription.resizeTextMaxSize = 18;

                var xpTitle = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$xp_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, 30f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                xpTitle.name = "xpModifiersTitle";

                var xpMod = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$xp_mod_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(150f, -80f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 16,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 500f,
                    height: 200f,
                    addContentSizeFitter: false);
                xpMod.name = "xpModifiersDesc";
                XPModifiersDescription = xpMod.GetComponent<Text>();
                XPModifiersDescription.resizeTextForBestFit = true;
                XPModifiersDescription.resizeTextMaxSize = 18;

                var lootTitle = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$loot_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, -130f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                lootTitle.name = "lootModifiersTitle";

                var lootDesc = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$loot_modifier_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(150f, -240f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 16,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 500f,
                    height: 200f,
                    addContentSizeFitter: false);
                lootDesc.name = "lootModifersDesc";
                LootModifersDescription = lootDesc.GetComponent<Text>();
                LootModifersDescription.resizeTextForBestFit = true;
                LootModifersDescription.resizeTextMaxSize = 18;

                var harvestTitle = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$harvest_header"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(100f, -260f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 22,
                    color: GUIManager.Instance.ValheimOrange,
                    outline: true,
                    outlineColor: Color.black,
                    width: 400f,
                    height: 40f,
                    addContentSizeFitter: false);
                harvestTitle.name = "harvestModifiersTitle";

                var harvestDesc = GUIManager.Instance.CreateText(
                    text: Localization.instance.Localize("$harvest_modifier_description"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(150f, -370f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 16,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 500f,
                    height: 200f,
                    addContentSizeFitter: false);
                harvestDesc.name = "harvestModifersDesc";
                HarvestModifiersDescription = harvestDesc.GetComponent<Text>();
                HarvestModifiersDescription.resizeTextForBestFit = true;
                HarvestModifiersDescription.resizeTextMaxSize = 18;

                selectChoiceButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$deathchoice_select"),
                    parent: DeathChoicePanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(-240f, -290f),
                    width: 200f,
                    height: 80f);
                Button bchoice = selectChoiceButton.GetComponent<Button>();
                //bchoice.interactable = true;
                bchoice.onClick.AddListener(MakePlayerDeathSelection);

                Logger.LogDebug("Setting up scroll entry");
                // Scroll area
                ChoicesScrollView = GUIManager.Instance.CreateScrollView(DeathChoicePanel.transform, false, true, 10f, 10f, GUIManager.Instance.ValheimScrollbarHandleColorBlock, Color.grey, 250f, 400f);
                ChoicesScrollView.transform.localPosition = new Vector2 { x = -260, y = -30 };
                ChoicesContent = ChoicesScrollView.GetComponentInChildren<ContentSizeFitter>().gameObject;
                ChoicesScrollView.GetComponentInChildren<ScrollRect>().scrollSensitivity = 200;
                choiceGroup = ChoicesContent.AddComponent<ToggleGroup>();

                Logger.LogDebug("Setting up death choice template entry");

                ChoicesContainer = Object.Instantiate(new GameObject("DeathChoice"), DeathChoicePanel.transform);
                RectTransform rt = ChoicesContainer.AddComponent<RectTransform>();
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
                rt.position = new Vector3(0, 0);
                //ChoicesContainer.transform.position = DeathChoicePanel.transform.position;
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
        }
    }
}
