using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static Deathlink.Common.DataObjects;
using static Deathlink.Death.DeathChoices;

namespace Deathlink.Death
{
    public static class Compendium
    {
        static string specialColor = "#ffa64d";
        //static string positiveColor = Color.green.ToString();

        private static GameObject changeChoiceButton;

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.UpdateTextsList))]
        public static class TextsDialog_UpdateTextsList_Patch
        {
            public static void Postfix(TextsDialog __instance) {
                AddDeathLinkExplanationPage(__instance);
            }

            private static void AddDeathLinkExplanationPage(TextsDialog textsDialog)
            {
                DeathChoiceLevel deathlinkPlayerSettings = DeathConfigurationData.playerDeathConfiguration;

                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"<size=48>{Localization.instance.Localize("$comp_header")}: <color={specialColor}>{deathlinkPlayerSettings.DisplayName}</color></size>");
                //sb.AppendLine($"<size=12><b>Deathlink</b> is a multiplayer mod where the death of one player causes the death of all other players.</size>");
                sb.AppendLine();

                sb.AppendLine($"<size=30><b>Death Effects</b></size>");
                sb.AppendLine(deathlinkPlayerSettings.GetDeathStyleDescription());
                sb.AppendLine();

                if (deathlinkPlayerSettings.DamageTakenModifier != 1f || deathlinkPlayerSettings.DamageDoneModifier != 1f) {
                    sb.AppendLine($"<size=30><b>Combat Modifiers</b></size>");
                    sb.AppendLine(deathlinkPlayerSettings.GetDamageModifierDescription());
                    sb.AppendLine();
                }

                if (deathlinkPlayerSettings.SkillModifiers != null && deathlinkPlayerSettings.SkillModifiers.Count > 0) {
                    sb.AppendLine($"<size=30><b>Skill Modifiers</b></size>");
                    sb.AppendLine(deathlinkPlayerSettings.GetSkillModiferDescription());
                    sb.AppendLine();
                }

                if (deathlinkPlayerSettings.ResourceModifiers != null && deathlinkPlayerSettings.ResourceModifiers.Count > 0) {
                    sb.AppendLine($"<size=30><b>Resource Modifiers</b></size>");
                    sb.AppendLine(deathlinkPlayerSettings.GetResourceModiferDescription());
                    sb.AppendLine();
                }

                if (deathlinkPlayerSettings.DeathLootModifiers != null && deathlinkPlayerSettings.DeathLootModifiers.Count > 0) {
                    sb.AppendLine($"<size=30><b>Loot Modifiers</b></size>");
                    sb.AppendLine(deathlinkPlayerSettings.GetLootModifiersDescription());
                    sb.AppendLine();
                }

                textsDialog.m_texts.Insert(0, new TextsDialog.TextInfo( Localization.instance.Localize($"$deathlink_settings"), Localization.instance.Localize(sb.ToString())));
            }
        }

        // Shows a "change my death choice" button at the top of the Deathlink compendium page,
        // visible only while that page is selected and the player still has a change available.
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new Type[] { typeof(TextsDialog.TextInfo) })]
        public static class TextsDialog_ShowText_Patch
        {
            public static void Postfix(TextsDialog __instance, TextsDialog.TextInfo text)
            {
                bool isDeathlinkPage = text != null && text.m_topic == Localization.instance.Localize("$deathlink_settings");
                bool canChange = isDeathlinkPage && DeathConfigurationData.PlayerCanChangeChoice(Player.m_localPlayer);
                Logger.LogDebug($"TextsDialog.ShowText topic='{text?.m_topic}' isDeathlinkPage={isDeathlinkPage} canChange={canChange}");

                if (!canChange) {
                    if (changeChoiceButton != null) { changeChoiceButton.SetActive(false); }
                    return;
                }

                EnsureChangeChoiceButton(__instance);
                if (changeChoiceButton != null) {
                    changeChoiceButton.SetActive(true);
                    changeChoiceButton.transform.SetAsLastSibling();
                }
            }

            private static void EnsureChangeChoiceButton(TextsDialog dialog)
            {
                if (changeChoiceButton != null) { return; }
                if (GUIManager.Instance == null) {
                    Logger.LogWarning("GUIManager not setup, skipping death choice change button creation.");
                    return;
                }

                Transform closebuttonTF = dialog.transform.Find("Texts_frame/Closebutton");

                changeChoiceButton = GUIManager.Instance.CreateButton(
                    text: Localization.instance.Localize("$comp_change_choice"),
                    parent: closebuttonTF,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(300f, 0f),
                    width: 200f,
                    height: 40f);
                changeChoiceButton.GetComponent<Button>().onClick.AddListener(() => {
                    // Keep the inventory open (cursor stays free) and just close the texts dialog
                    // before opening the change panel over the inventory.
                    dialog.OnClose();
                    DeathChoiceUI.Instance.ShowForChange();
                });

                // position adjustment
                // changeChoiceButton
            }

            // Finds the compendium's close button. Prefers the prefab-wired OnClose listener, then
            // falls back to a child whose name contains "close".
            private static RectTransform FindCloseButton(TextsDialog dialog)
            {
                Button byName = null;
                foreach (Button b in dialog.GetComponentsInChildren<Button>(true)) {
                    if (b.gameObject == changeChoiceButton) { continue; }
                    for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++) {
                        if (b.onClick.GetPersistentMethodName(i) == "OnClose") {
                            return b.GetComponent<RectTransform>();
                        }
                    }
                    if (byName == null && b.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0) {
                        byName = b;
                    }
                }
                return byName != null ? byName.GetComponent<RectTransform>() : null;
            }
        }
    }
}
