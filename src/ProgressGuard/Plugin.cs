using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ProgressGuard
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.progressguard.valheim";
        public const string PluginName = "ProgressGuard";
        public const string PluginVersion = "1.0.0";

        public static Plugin Instance { get; private set; }

        public static ConfigEntry<int> MinPlayersRequired { get; private set; }
        public static ConfigEntry<int> MinAgreesRequired { get; private set; }
        public static ConfigEntry<int> VoteTimeoutSeconds { get; private set; }
        public static ConfigEntry<bool> DebugLogging { get; private set; }

        internal static VoteManager Votes
        {
            get { return Instance != null ? Instance._voteManager : null; }
        }

        private Harmony _harmony;
        private VoteManager _voteManager;

        private void Awake()
        {
            Instance = this;
            InitializeConfig();

            _voteManager = new VoteManager();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo(string.Format("{0} {1} loaded.", PluginName, PluginVersion));
            Logger.LogInfo("Server/host enforcement enabled for boss summon voting.");
        }

        private void Update()
        {
            if (_voteManager != null)
            {
                _voteManager.Update();
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }

        private void InitializeConfig()
        {
            MinPlayersRequired = Config.Bind("General", "MinPlayersRequired", 2, "Minimum online players required before an undefeated boss can be summoned.");
            MinAgreesRequired = Config.Bind("General", "MinAgreesRequired", 2, "Minimum unique /agree votes required to unlock the next summon attempt.");
            VoteTimeoutSeconds = Config.Bind("General", "VoteTimeoutSeconds", 60, "How many seconds a vote remains active before it expires.");
            DebugLogging = Config.Bind("General", "DebugLogging", true, "Enable verbose debug logging for summon and chat hooks.");
        }

        public static bool IsAuthoritativeServer()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        public static void LogDebug(string message)
        {
            if (Instance != null && DebugLogging != null && DebugLogging.Value)
            {
                Instance.Logger.LogInfo("[Debug] " + message);
            }
        }

        public static void LogInfo(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogInfo(message);
            }
        }

        public static void LogWarning(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogWarning(message);
            }
        }

        public static void LogError(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogError(message);
            }
        }

        public static void LogException(string context, Exception exception)
        {
            if (Instance != null)
            {
                Instance.Logger.LogError(string.Format("{0}: {1}", context, exception));
            }
        }

        public static void Broadcast(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            LogInfo("[Broadcast] " + message);

            try
            {
                if (Chat.instance != null)
                {
                    Chat.instance.SendText(Talker.Type.Shout, message);
                }
            }
            catch (Exception exception)
            {
                LogWarning("Chat broadcast failed: " + exception.Message);
            }

            try
            {
                BroadcastHudMessage(message);
            }
            catch (Exception exception)
            {
                LogWarning("HUD broadcast failed: " + exception.Message);
            }
        }

        private static void BroadcastHudMessage(string message)
        {
            List<Player> players = Player.GetAllPlayers();
            if (players == null || players.Count == 0)
            {
                return;
            }

            Type messageType = AccessTools.TypeByName("MessageHud+MessageType");
            if (messageType == null)
            {
                return;
            }

            object centerValue = Enum.Parse(messageType, "Center");
            MethodInfo messageMethod = AccessTools.Method(typeof(Player), "Message", new[] { messageType, typeof(string), typeof(int), typeof(Sprite) });
            if (messageMethod == null)
            {
                return;
            }

            foreach (Player player in players)
            {
                if (player == null)
                {
                    continue;
                }

                messageMethod.Invoke(player, new object[] { centerValue, message, 0, null });
            }
        }
    }
}
