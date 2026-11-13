using Deathlink.Common;
using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Deathlink.Common.DataObjects;
using static Deathlink.Death.DeathChoices;

namespace Deathlink.Death
{
    public static class Compendium
    {
        private const string SpecialColor = "#ffa64d";

        private static GameObject changeChoiceButton;
        private static GameObject leaderboardButton;

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

                sb.AppendLine($"<size=48>{Localization.instance.Localize("$comp_header")}: <color={SpecialColor}>{deathlinkPlayerSettings.DisplayName}</color></size>");
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

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new Type[] { typeof(TextsDialog.TextInfo) })]
        public static class TextsDialog_ShowText_Patch
        {
            public static void Postfix(TextsDialog __instance, TextsDialog.TextInfo text)
            {
                bool isDeathlinkPage = text != null && text.m_topic == Localization.instance.Localize("$deathlink_settings");
                bool canChange = isDeathlinkPage && DeathConfigurationData.PlayerCanChangeChoice(Player.m_localPlayer);
                Logger.LogDebug($"TextsDialog.ShowText topic='{text?.m_topic}' isDeathlinkPage={isDeathlinkPage} canChange={canChange}");

                ShowCloseButtonSibling(ref changeChoiceButton, __instance, canChange, "$comp_change_choice", 300f, () => {
                    // Close only the texts dialog, so the inventory stays open (and the cursor free) under the change panel.
                    __instance.OnClose();
                    DeathChoiceUI.Instance.ShowForChange();
                });

                bool showLeaderboard = ValConfig.EnableLeaderboard.Value && isDeathlinkPage;
                ShowCloseButtonSibling(ref leaderboardButton, __instance, showLeaderboard, "$leaderboard", -250f, LeaderboardUI.Show);
                if (!showLeaderboard) { LeaderboardUI.Hide(); }
            }

            private static void ShowCloseButtonSibling(ref GameObject button, TextsDialog dialog, bool show, string locKey, float x, UnityAction onClick)
            {
                if (!show) {
                    if (button != null) { button.SetActive(false); }
                    return;
                }

                if (button == null) {
                    Transform closebuttonTF = dialog.transform.Find("Texts_frame/Closebutton");
                    if (closebuttonTF == null) { return; }

                    button = GUIManager.Instance.CreateButton(
                        text: Localization.instance.Localize(locKey),
                        parent: closebuttonTF,
                        anchorMin: new Vector2(0.5f, 0.5f),
                        anchorMax: new Vector2(0.5f, 0.5f),
                        position: new Vector2(x, 0f),
                        width: 200f,
                        height: 40f);
                    button.GetComponent<Button>().onClick.AddListener(onClick);
                }
                button.SetActive(true);
                button.transform.SetAsLastSibling();
            }
        }
    }
}
