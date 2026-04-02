using System;
using HarmonyLib;
using UnityEngine;

namespace ProgressGuard
{
    internal static class ChatCommandHandler
    {
        private const string AgreeCommand = "/agree";
        private const string AgreeToken = "agree";

        public static bool TryHandleRemoteMessage(long senderId, UserInfo userInfo, string text, string source)
        {
            if (!Plugin.IsAuthoritativeServer())
            {
                return false;
            }

            if (!IsAgreeCommand(text))
            {
                return false;
            }

            string playerName = GetDisplayName(userInfo);
            string playerId = BuildPlayerId(senderId, playerName);
            Plugin.LogDebug(string.Format("Handling agree command '{0}' from {1} ({2}) via {3}.", text, playerName, playerId, source));
            HandleAgreement(playerId, playerName);
            return true;
        }

        public static bool TryHandleLocalMessage(string text, string source)
        {
            if (!Plugin.IsAuthoritativeServer())
            {
                return false;
            }

            if (!IsAgreeCommand(text))
            {
                return false;
            }

            UserInfo localUser = UserInfo.GetLocalUser();
            long senderId = ZNet.instance != null ? ZNet.GetUID() : 0L;
            string playerName = GetDisplayName(localUser);
            string playerId = BuildPlayerId(senderId, playerName);
            Plugin.LogDebug(string.Format("Handling local agree command '{0}' from {1} ({2}) via {3}.", text, playerName, playerId, source));
            HandleAgreement(playerId, playerName);
            return true;
        }

        private static void HandleAgreement(string playerId, string playerName)
        {
            VoteManager.AgreementResult result = Plugin.Votes.RegisterAgreement(playerId, playerName);
            switch (result.Status)
            {
                case VoteManager.AgreementStatus.NoActiveVote:
                    Plugin.LogInfo(string.Format("Ignored {0} from {1}; no active vote.", AgreeCommand, playerName));
                    break;
                case VoteManager.AgreementStatus.Expired:
                    Plugin.Broadcast("Vote expired.");
                    break;
                case VoteManager.AgreementStatus.Duplicate:
                    Plugin.LogInfo(string.Format("Ignored duplicate {0} from {1}.", AgreeCommand, playerName));
                    break;
                case VoteManager.AgreementStatus.Added:
                    Plugin.Broadcast(string.Format("{0} agreed ({1}/{2}).", playerName, result.AgreeCount, result.RequiredAgrees));
                    break;
                case VoteManager.AgreementStatus.Approved:
                    Plugin.Broadcast("Boss summon approved. Try again.");
                    break;
            }
        }

        private static bool IsAgreeCommand(string text)
        {
            string normalized = NormalizeCommand(text);
            return string.Equals(normalized, AgreeToken, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCommand(string text)
        {
            string normalized = (text ?? string.Empty).Trim();

            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1).TrimStart();
            }

            return normalized;
        }

        private static string BuildPlayerId(long senderId, string playerName)
        {
            if (senderId != 0L)
            {
                return senderId.ToString();
            }

            string safeName = string.IsNullOrWhiteSpace(playerName) ? "unknown" : playerName.Trim().ToLowerInvariant();
            return "name:" + safeName;
        }

        private static string GetDisplayName(UserInfo userInfo)
        {
            if (userInfo == null)
            {
                return "Unknown";
            }

            string displayName = userInfo.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return string.IsNullOrWhiteSpace(userInfo.Name) ? "Unknown" : userInfo.Name;
        }
    }

    [HarmonyPatch(typeof(Chat), "RPC_ChatMessage")]
    internal static class ChatRpcChatMessagePatch
    {
        private static bool Prefix(long sender, Vector3 position, int type, UserInfo userInfo, string text)
        {
            if (ChatCommandHandler.TryHandleRemoteMessage(sender, userInfo, text, "Chat.RPC_ChatMessage"))
            {
                Plugin.LogDebug("Blocked original chat handling for /agree RPC command.");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
    internal static class ChatSendTextPatch
    {
        private static bool Prefix(Talker.Type type, string text)
        {
            if (ChatCommandHandler.TryHandleLocalMessage(text, "Chat.SendText"))
            {
                Plugin.LogDebug("Blocked original local chat send for /agree command.");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Talker), "RPC_Say")]
    internal static class TalkerRpcSayPatch
    {
        private static bool Prefix(long sender, int ctype, UserInfo user, string text)
        {
            if (ChatCommandHandler.TryHandleRemoteMessage(sender, user, text, "Talker.RPC_Say"))
            {
                Plugin.LogDebug("Blocked original Talker.RPC_Say handling for agree command.");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Talker), nameof(Talker.Say))]
    internal static class TalkerSayPatch
    {
        private static bool Prefix(Talker.Type type, string text)
        {
            if (ChatCommandHandler.TryHandleLocalMessage(text, "Talker.Say"))
            {
                Plugin.LogDebug("Blocked original Talker.Say handling for local agree command.");
                return false;
            }

            return true;
        }
    }
}
