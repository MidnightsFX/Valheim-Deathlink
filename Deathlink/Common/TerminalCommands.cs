using Jotunn.Entities;
using Jotunn.Managers;
using System;

namespace Deathlink.Common
{
    internal static class TerminalCommands
    {
        internal static void AddCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new ResetPlayerDeathChoice());
        }

        internal class ResetPlayerDeathChoice : ConsoleCommand
        {
            public override string Name => "DL-RESET-CHOICE";

            public override bool IsNetwork => true;
            public override string Help => "Format: [steamID|playerName] resets the deathlink choice for the target player. Accepts a platform Steam ID or a case-insensitive player name.";

            public override void Run(string[] args)
            {
                if (SynchronizationManager.Instance.PlayerIsAdmin == false) {
                    Logger.LogWarning("You are not an admin, and not allowed to run this command.");
                    return;
                }

                if (args.Length < 1) {
                    Logger.LogInfo("Player Steam ID or name required");
                    return;
                }

                string inputArg = args[0];

                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList()) {
                    string platformUserID = player.m_userInfo.m_id.m_userID.ToString();
                    string displayName = player.m_userInfo.m_displayName;

                    bool idMatch = platformUserID == inputArg;
                    bool accountNameMatch = string.Equals(displayName, inputArg, StringComparison.OrdinalIgnoreCase);
                    bool playerNameMatch = string.Equals(player.m_name, inputArg, StringComparison.OrdinalIgnoreCase);

                    Logger.LogDebug($"Checking Player {displayName} (platformID: {platformUserID}, playerName: {player.m_name}) against '{inputArg}'");

                    if (idMatch || accountNameMatch || playerNameMatch) {
                        Logger.LogInfo($"Matched player {displayName} (platformID: {platformUserID}) for reset command.");
                        ZPackage resetpkg = new ZPackage();
                        resetpkg.Write(platformUserID);
                        Logger.LogDebug("Requesting server reset of player saved Deathlink choice");
                        ValConfig.resetChoiceRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), resetpkg);
                    }
                }
            }
        }
    }
}
