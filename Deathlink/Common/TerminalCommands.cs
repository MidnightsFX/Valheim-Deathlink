using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections.Generic;

namespace Deathlink.Common
{
    internal static class TerminalCommands
    {
        internal static void AddCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new ResetPlayerDeathChoice());
        }

        // Every hop is a per-connection ZRpc, not a routed RPC whose sender any client can forge: the server
        // reads the admin from the request's socket, and a client only accepts a reset from the server.
        internal class ResetPlayerDeathChoice : ConsoleCommand
        {
            private const string RequestRpc = Deathlink.PluginGUID + ".ResetChoiceRequest";
            private const string ResetRpc = Deathlink.PluginGUID + ".ResetChoice";
            private const string ReplyRpc = Deathlink.PluginGUID + ".ResetChoiceReply";

            // Where the server's reply, a round trip later, is printed.
            private static Terminal output;

            public override string Name => "DL-RESET-CHOICE";

            public override bool IsNetwork => true;
            public override string Help => "Format: [steamID|playerName] resets the deathlink choice for the target player. Accepts a platform Steam ID or a case-insensitive player name.";

            public override void Run(string[] args)
            {
                Run(args, null);
            }

            public override void Run(string[] args, Terminal context)
            {
                output = context;
                if (ZNet.instance == null) {
                    Print("Join a world first.");
                    return;
                }
                if (args.Length < 1) {
                    Print("Player Steam ID or name required");
                    return;
                }

                string inputArg = args[0];
                // By character: one account can be playing more than one character (it is how tests run).
                List<ZDOID> targets = new List<ZDOID>();
                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList()) {
                    string platformUserID = player.m_userInfo.m_id.m_userID.ToString();
                    string displayName = player.m_userInfo.m_displayName;

                    bool idMatch = platformUserID == inputArg;
                    bool accountNameMatch = string.Equals(displayName, inputArg, StringComparison.OrdinalIgnoreCase);
                    bool playerNameMatch = string.Equals(player.m_name, inputArg, StringComparison.OrdinalIgnoreCase);

                    Logger.LogDebug($"Checking Player {displayName} (platformID: {platformUserID}, playerName: {player.m_name}) against '{inputArg}'");

                    if (idMatch || accountNameMatch || playerNameMatch) {
                        if (player.m_characterID == ZDOID.None) {
                            Print($"{player.m_name} has no character in the world right now.");
                            continue;
                        }
                        Logger.LogInfo($"Matched player {displayName} (platformID: {platformUserID}, character {player.m_name}) for reset command.");
                        targets.Add(player.m_characterID);
                    }
                }
                if (targets.Count == 0) {
                    Print($"No player online matches '{inputArg}'.");
                    return;
                }

                if (ZNet.instance.IsServer()) {
                    // Singleplayer, or the host of a listen server: always an admin, nobody to ask.
                    foreach (ZDOID character in targets) { Print(ResetChoice(character)); }
                    return;
                }

                // Only for a quicker, clearer answer. The server makes the real check.
                if (SynchronizationManager.Instance.PlayerIsAdmin == false) {
                    Print("You are not an admin, and not allowed to run this command.");
                    return;
                }
                ZRpc server = ZNet.instance.GetServerRPC();
                if (server == null) {
                    Print("Not connected to a server.");
                    return;
                }
                foreach (ZDOID character in targets) {
                    Logger.LogDebug("Requesting server reset of player saved Deathlink choice");
                    ZPackage request = new ZPackage();
                    request.Write(character);
                    server.Invoke(RequestRpc, request);
                }
            }

            // Runs on both ends of every connection: on the server once per client, on a client once for its server.
            [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
            internal static class RegisterResetRpcs
            {
                private static void Postfix(ZNet __instance, ZNetPeer peer)
                {
                    if (__instance.IsServer()) {
                        peer.m_rpc.Register<ZPackage>(RequestRpc, OnRequest);
                    } else {
                        peer.m_rpc.Register(ResetRpc, OnReset);
                        peer.m_rpc.Register<string>(ReplyRpc, OnReply);
                    }
                }
            }

            private static void OnRequest(ZRpc rpc, ZPackage request)
            {
                ZNetPeer requester = ZNet.instance.GetPeer(rpc);
                // Not ready means the handshake has not finished, so this is not yet a player in the world.
                if (requester == null || requester.IsReady() == false) { return; }
                ZDOID character = request.ReadZDOID();

                string hostName = rpc.GetSocket().GetHostName();
                if (ZNet.instance.IsAdmin(hostName) == false) {
                    Logger.LogWarning($"Rejecting a Deathlink choice reset for character {character} from non-admin {requester.m_playerName} ({hostName}).");
                    rpc.Invoke(ReplyRpc, "Only server admins can reset a Deathlink choice.");
                    return;
                }

                Logger.LogInfo($"Received reset request for character {character} from admin {requester.m_playerName} ({hostName}).");
                rpc.Invoke(ReplyRpc, ResetChoice(character));
            }

            // Does no admin check of its own. Returns the message for the admin.
            private static string ResetChoice(ZDOID characterID)
            {
                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList()) {
                    if (player.m_characterID != characterID) { continue; }

                    ZDO localCharacter = Player.m_localPlayer != null ? Player.m_localPlayer.m_nview.GetZDO() : null;
                    if (localCharacter != null && player.m_characterID == localCharacter.m_uid) {
                        // The host's own character: there is no connection to forward to.
                        DeathConfigurationData.ResetLocalPlayerChoice();
                        return $"Reset the Deathlink choice of {player.m_name}.";
                    }

                    // In yaml mode the server's file holds the choice, keyed by the player id on the character.
                    ZDO character = ZDOMan.instance.GetZDO(player.m_characterID);
                    long playerID = character != null ? character.GetLong(ZDOVars.s_playerID, 0L) : 0L;
                    if (playerID != 0L && DeathConfigurationData.playerSettings.Remove(playerID)) {
                        DeathConfigurationData.WritePlayerChoices();
                    }

                    foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                        if (peer.m_characterID != player.m_characterID) { continue; }
                        peer.m_rpc.Invoke(ResetRpc);
                        return $"Sent a Deathlink choice reset to {player.m_name}.";
                    }
                    return $"{player.m_name} has no character in the world right now, so no reset was delivered.";
                }
                return "That player is no longer in the world, so no reset was delivered.";
            }

            private static void OnReset(ZRpc rpc)
            {
                Logger.LogInfo("Reset requested for local players death choice.");
                DeathConfigurationData.ResetLocalPlayerChoice();
            }

            private static void OnReply(ZRpc rpc, string message)
            {
                Print(message);
            }

            private static void Print(string message)
            {
                Logger.LogInfo(message);
                // Unity's null check, so a terminal destroyed since the command was typed is skipped.
                Terminal terminal = output != null ? output : global::Console.instance;
                if (terminal != null) { terminal.AddString(message); }
            }
        }
    }
}
