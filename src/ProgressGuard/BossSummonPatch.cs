using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ProgressGuard
{
    internal static class BossSummonPatch
    {
        private const float DuplicateDecisionWindowSeconds = 0.35f;
        private static readonly Dictionary<int, RecentDecision> RecentDecisions = new Dictionary<int, RecentDecision>();

        internal static bool EvaluateSummonAttempt(OfferingBowl bowl, string source, long senderId)
        {
            if (!Plugin.IsAuthoritativeServer())
            {
                return true;
            }

            if (bowl == null)
            {
                Plugin.LogWarning(string.Format("Received summon gate callback from {0} with null OfferingBowl.", source));
                return true;
            }

            BossProgressHelper.BossContext boss = BossProgressHelper.ResolveBossContext(bowl);
            if (TryReuseRecentDecision(bowl, boss, out bool allowFromCache))
            {
                Plugin.LogDebug(string.Format("Reusing cached summon decision for {0} via {1}: allow={2}.", boss.DisplayName, source, allowFromCache));
                return allowFromCache;
            }

            int onlinePlayers = GetOnlinePlayerCount();
            Plugin.LogDebug(string.Format(
                "Summon attempt detected via {0}. boss={1}, defeatedKey={2}, senderId={3}, onlinePlayers={4}.",
                source,
                boss.DisplayName,
                boss.DefeatedGlobalKey,
                senderId,
                onlinePlayers));

            if (BossProgressHelper.IsBossDefeated(boss))
            {
                Plugin.Votes.ClearApproval(boss);
                Plugin.LogInfo(string.Format("Allowing summon for {0}; boss is already defeated.", boss.DisplayName));
                RememberDecision(bowl, boss, true);
                return true;
            }

            int minPlayers = Math.Max(1, Plugin.MinPlayersRequired.Value);
            if (onlinePlayers < minPlayers)
            {
                Plugin.LogWarning(string.Format("Blocking summon for {0}; only {1}/{2} players online.", boss.DisplayName, onlinePlayers, minPlayers));
                Plugin.Broadcast(string.Format("Boss summon locked. Need at least {0} players online.", minPlayers));
                RememberDecision(bowl, boss, false);
                return false;
            }

            if (Plugin.Votes.TryConsumeApproval(boss))
            {
                Plugin.LogInfo(string.Format("Allowing one approved summon attempt for {0}.", boss.DisplayName));
                RememberDecision(bowl, boss, true);
                return true;
            }

            bool startedNewVote;
            Plugin.Votes.StartVote(boss, out startedNewVote);
            Plugin.Broadcast("Boss summon locked. Type agree or /agree to approve.");
            Plugin.LogInfo(string.Format("Blocked summon for {0}; vote required before next summon attempt.", boss.DisplayName));
            RememberDecision(bowl, boss, false);
            return false;
        }

        private static int GetOnlinePlayerCount()
        {
            int znetCount = 0;
            if (ZNet.instance != null)
            {
                try
                {
                    znetCount = ZNet.instance.GetNrOfPlayers();
                }
                catch (Exception exception)
                {
                    Plugin.LogWarning("Failed to read ZNet player count: " + exception.Message);
                }
            }

            int playerListCount = 0;
            try
            {
                List<Player> players = Player.GetAllPlayers();
                playerListCount = players != null ? players.Count : 0;
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("Failed to read Player.GetAllPlayers count: " + exception.Message);
            }

            return Math.Max(znetCount, playerListCount);
        }

        private static bool TryReuseRecentDecision(OfferingBowl bowl, BossProgressHelper.BossContext boss, out bool allow)
        {
            CleanupRecentDecisions();

            int instanceId = bowl.GetInstanceID();
            RecentDecision recentDecision;
            if (RecentDecisions.TryGetValue(instanceId, out recentDecision)
                && recentDecision != null
                && Time.unscaledTime - recentDecision.Timestamp <= DuplicateDecisionWindowSeconds
                && string.Equals(recentDecision.ApprovalKey, boss.ApprovalKey, StringComparison.OrdinalIgnoreCase))
            {
                allow = recentDecision.Allow;
                return true;
            }

            allow = true;
            return false;
        }

        private static void RememberDecision(OfferingBowl bowl, BossProgressHelper.BossContext boss, bool allow)
        {
            CleanupRecentDecisions();
            RecentDecisions[bowl.GetInstanceID()] = new RecentDecision
            {
                ApprovalKey = boss.ApprovalKey,
                Allow = allow,
                Timestamp = Time.unscaledTime
            };
        }

        private static void CleanupRecentDecisions()
        {
            List<int> expiredKeys = null;
            foreach (KeyValuePair<int, RecentDecision> entry in RecentDecisions)
            {
                if (Time.unscaledTime - entry.Value.Timestamp > DuplicateDecisionWindowSeconds)
                {
                    if (expiredKeys == null)
                    {
                        expiredKeys = new List<int>();
                    }

                    expiredKeys.Add(entry.Key);
                }
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (int index = 0; index < expiredKeys.Count; index++)
            {
                RecentDecisions.Remove(expiredKeys[index]);
            }
        }

        private sealed class RecentDecision
        {
            public string ApprovalKey;
            public bool Allow;
            public float Timestamp;
        }
    }

    [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.Interact))]
    internal static class OfferingBowlInteractTracePatch
    {
        private static void Prefix(OfferingBowl __instance, Humanoid user, bool hold, bool alt)
        {
            BossProgressHelper.BossContext boss = BossProgressHelper.ResolveBossContext(__instance);
            Plugin.LogDebug(string.Format(
                "OfferingBowl.Interact called. altar={0}, boss={1}, user={2}, hold={3}, alt={4}.",
                __instance != null && __instance.gameObject != null ? __instance.gameObject.name : "null",
                boss.DisplayName,
                user != null ? user.name : "Unknown",
                hold,
                alt));
        }

        private static void Postfix(bool __result, OfferingBowl __instance)
        {
            BossProgressHelper.BossContext boss = BossProgressHelper.ResolveBossContext(__instance);
            Plugin.LogDebug(string.Format("OfferingBowl.Interact completed for {0} with result={1}.", boss.DisplayName, __result));
        }
    }

    [HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.UseItem))]
    internal static class OfferingBowlUseItemTracePatch
    {
        private static void Prefix(OfferingBowl __instance, Humanoid user, ItemDrop.ItemData item)
        {
            BossProgressHelper.BossContext boss = BossProgressHelper.ResolveBossContext(__instance);
            string itemName = item != null && item.m_dropPrefab != null ? item.m_dropPrefab.name : "UnknownItem";
            Plugin.LogDebug(string.Format(
                "OfferingBowl.UseItem called. altar={0}, boss={1}, user={2}, item={3}.",
                __instance != null && __instance.gameObject != null ? __instance.gameObject.name : "null",
                boss.DisplayName,
                user != null ? user.name : "Unknown",
                itemName));
        }

        private static void Postfix(bool __result, OfferingBowl __instance)
        {
            BossProgressHelper.BossContext boss = BossProgressHelper.ResolveBossContext(__instance);
            Plugin.LogDebug(string.Format("OfferingBowl.UseItem completed for {0} with result={1}.", boss.DisplayName, __result));
        }
    }

    // Valheim altar implementations can shift between versions.
    // For this inspected build, RPC_SpawnBoss and InitiateSpawnBoss are the strongest pre-spawn gate points.
    // Interact and UseItem remain patched only for trace logging to help retarget if altar flow changes.
    [HarmonyPatch(typeof(OfferingBowl), "RPC_SpawnBoss")]
    internal static class OfferingBowlRpcSpawnBossPatch
    {
        private static bool Prefix(OfferingBowl __instance, long senderId, Vector3 point, bool removeItemsFromInventory)
        {
            Plugin.LogDebug(string.Format("OfferingBowl.RPC_SpawnBoss called. senderId={0}, removeItems={1}, point={2}.", senderId, removeItemsFromInventory, point));
            return BossSummonPatch.EvaluateSummonAttempt(__instance, "OfferingBowl.RPC_SpawnBoss", senderId);
        }
    }

    [HarmonyPatch(typeof(OfferingBowl), "InitiateSpawnBoss")]
    internal static class OfferingBowlInitiateSpawnBossPatch
    {
        private static bool Prefix(OfferingBowl __instance, Vector3 point, bool removeItemsFromInventory)
        {
            Humanoid interactUser = BossProgressHelper.TryGetInteractUser(__instance);
            string interactUserName = interactUser != null ? interactUser.name : "Unknown";
            Plugin.LogDebug(string.Format(
                "OfferingBowl.InitiateSpawnBoss called. interactUser={0}, removeItems={1}, point={2}.",
                interactUserName,
                removeItemsFromInventory,
                point));
            return BossSummonPatch.EvaluateSummonAttempt(__instance, "OfferingBowl.InitiateSpawnBoss", 0L);
        }
    }
}
