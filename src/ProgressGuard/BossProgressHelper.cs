using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ProgressGuard
{
    internal static class BossProgressHelper
    {
        private static readonly List<BossDescriptor> BossMappings = new List<BossDescriptor>
        {
            new BossDescriptor("eikthyr", "Eikthyr", "defeated_eikthyr", "eikthyr"),
            new BossDescriptor("elder", "The Elder", "defeated_gdking", "gdking", "elder"),
            new BossDescriptor("bonemass", "Bonemass", "defeated_bonemass", "bonemass"),
            new BossDescriptor("moder", "Moder", "defeated_dragon", "dragon", "moder"),
            new BossDescriptor("yagluth", "Yagluth", "defeated_goblinking", "goblinking", "yagluth"),
            new BossDescriptor("queen", "The Queen", "defeated_queen", "queen", "seekerqueen"),
            new BossDescriptor("fader", "Fader", "defeated_fader", "fader")
        };

        public static BossContext ResolveBossContext(OfferingBowl bowl)
        {
            string altarName = SafeName(bowl != null && bowl.gameObject != null ? bowl.gameObject.name : string.Empty);
            string displayName = SafeName(GetStringFieldValue(bowl, "m_name"));
            string configuredKey = SafeName(GetStringFieldValue(bowl, "m_setGlobalKey"));
            string bossPrefabName = SafeName(GetGameObjectFieldValue(bowl, "m_bossPrefab"));
            string bossItemName = SafeName(GetItemDropFieldValue(bowl, "m_bossItem"));
            string itemPrefabName = SafeName(GetItemDropFieldValue(bowl, "m_itemPrefab"));

            List<string> candidates = new List<string>();
            AddCandidate(candidates, bossPrefabName);
            AddCandidate(candidates, displayName);
            AddCandidate(candidates, altarName);
            AddCandidate(candidates, bossItemName);
            AddCandidate(candidates, itemPrefabName);
            AddCandidate(candidates, configuredKey);

            foreach (BossDescriptor descriptor in BossMappings)
            {
                if (descriptor.Matches(candidates))
                {
                    return new BossContext(
                        descriptor.Id,
                        descriptor.DisplayName,
                        descriptor.DefeatedGlobalKey,
                        bossPrefabName,
                        altarName,
                        configuredKey);
                }
            }

            if (!string.IsNullOrEmpty(configuredKey) && configuredKey.StartsWith("defeated_", StringComparison.OrdinalIgnoreCase))
            {
                return new BossContext(
                    configuredKey,
                    ToDisplayName(configuredKey),
                    configuredKey,
                    bossPrefabName,
                    altarName,
                    configuredKey);
            }

            if (!string.IsNullOrEmpty(bossPrefabName))
            {
                return new BossContext(
                    bossPrefabName,
                    ToDisplayName(bossPrefabName),
                    GuessDefeatedKey(bossPrefabName),
                    bossPrefabName,
                    altarName,
                    configuredKey);
            }

            string fallbackId = !string.IsNullOrEmpty(altarName) ? altarName : "unknown_boss";
            return new BossContext(
                fallbackId,
                ToDisplayName(fallbackId),
                GuessDefeatedKey(fallbackId),
                bossPrefabName,
                altarName,
                configuredKey);
        }

        public static bool IsBossDefeated(BossContext boss)
        {
            if (boss == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(boss.DefeatedGlobalKey))
            {
                Plugin.LogWarning(string.Format("No defeated global key mapping found for boss context {0}. Treating as undefeated.", boss.DisplayName));
                return false;
            }

            if (ZoneSystem.instance == null)
            {
                Plugin.LogWarning(string.Format("ZoneSystem.instance is null while checking boss key {0}. Treating as undefeated.", boss.DefeatedGlobalKey));
                return false;
            }

            bool defeated = ZoneSystem.instance.GetGlobalKey(boss.DefeatedGlobalKey);
            Plugin.LogDebug(string.Format("Boss defeated check for {0} ({1}) returned {2}.", boss.DisplayName, boss.DefeatedGlobalKey, defeated));
            return defeated;
        }

        public static Humanoid TryGetInteractUser(OfferingBowl bowl)
        {
            if (bowl == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(typeof(OfferingBowl), "m_interactUser");
            if (field == null)
            {
                return null;
            }

            return field.GetValue(bowl) as Humanoid;
        }

        private static void AddCandidate(List<string> candidates, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                candidates.Add(value.ToLowerInvariant());
            }
        }

        private static string GetStringFieldValue(OfferingBowl bowl, string fieldName)
        {
            if (bowl == null)
            {
                return string.Empty;
            }

            FieldInfo field = AccessTools.Field(typeof(OfferingBowl), fieldName);
            if (field == null)
            {
                return string.Empty;
            }

            object rawValue = field.GetValue(bowl);
            return rawValue as string ?? string.Empty;
        }

        private static string GetGameObjectFieldValue(OfferingBowl bowl, string fieldName)
        {
            if (bowl == null)
            {
                return string.Empty;
            }

            FieldInfo field = AccessTools.Field(typeof(OfferingBowl), fieldName);
            if (field == null)
            {
                return string.Empty;
            }

            GameObject gameObject = field.GetValue(bowl) as GameObject;
            return gameObject != null ? gameObject.name : string.Empty;
        }

        private static string GetItemDropFieldValue(OfferingBowl bowl, string fieldName)
        {
            if (bowl == null)
            {
                return string.Empty;
            }

            FieldInfo field = AccessTools.Field(typeof(OfferingBowl), fieldName);
            if (field == null)
            {
                return string.Empty;
            }

            ItemDrop itemDrop = field.GetValue(bowl) as ItemDrop;
            return itemDrop != null ? itemDrop.name : string.Empty;
        }

        private static string GuessDefeatedKey(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string normalized = token.ToLowerInvariant();
            for (int index = 0; index < BossMappings.Count; index++)
            {
                if (BossMappings[index].Matches(new[] { normalized }))
                {
                    return BossMappings[index].DefeatedGlobalKey;
                }
            }

            if (normalized.StartsWith("defeated_", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return string.Empty;
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace("(Clone)", string.Empty).Trim();
        }

        private static string ToDisplayName(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "Unknown Boss";
            }

            string clean = token.Replace("defeated_", string.Empty).Replace('_', ' ').Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return "Unknown Boss";
            }

            return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
        }

        internal sealed class BossContext
        {
            public BossContext(string id, string displayName, string defeatedGlobalKey, string bossPrefabName, string altarName, string configuredGlobalKey)
            {
                Id = string.IsNullOrWhiteSpace(id) ? "unknown_boss" : id;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unknown Boss" : displayName;
                DefeatedGlobalKey = defeatedGlobalKey ?? string.Empty;
                BossPrefabName = bossPrefabName ?? string.Empty;
                AltarName = altarName ?? string.Empty;
                ConfiguredGlobalKey = configuredGlobalKey ?? string.Empty;
            }

            public string Id { get; private set; }
            public string DisplayName { get; private set; }
            public string DefeatedGlobalKey { get; private set; }
            public string BossPrefabName { get; private set; }
            public string AltarName { get; private set; }
            public string ConfiguredGlobalKey { get; private set; }

            public string ApprovalKey
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(DefeatedGlobalKey))
                    {
                        return DefeatedGlobalKey;
                    }

                    return Id;
                }
            }
        }

        private sealed class BossDescriptor
        {
            public BossDescriptor(string id, string displayName, string defeatedGlobalKey, params string[] matchTokens)
            {
                Id = id;
                DisplayName = displayName;
                DefeatedGlobalKey = defeatedGlobalKey;
                MatchTokens = new List<string>(matchTokens);
            }

            public string Id { get; private set; }
            public string DisplayName { get; private set; }
            public string DefeatedGlobalKey { get; private set; }
            public List<string> MatchTokens { get; private set; }

            public bool Matches(IEnumerable<string> candidates)
            {
                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    string normalizedCandidate = candidate.ToLowerInvariant();
                    for (int index = 0; index < MatchTokens.Count; index++)
                    {
                        if (normalizedCandidate.Contains(MatchTokens[index]))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
