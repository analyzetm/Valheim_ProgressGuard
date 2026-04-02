using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProgressGuard
{
    internal sealed class VoteManager
    {
        private readonly object _sync = new object();
        private VoteSession _activeVote;
        private BossProgressHelper.BossContext _pendingApproval;

        public bool StartVote(BossProgressHelper.BossContext boss, out bool startedNewSession)
        {
            lock (_sync)
            {
                float expiresAt = Time.unscaledTime + Math.Max(5, Plugin.VoteTimeoutSeconds.Value);

                if (_activeVote != null
                    && string.Equals(_activeVote.Boss.ApprovalKey, boss.ApprovalKey, StringComparison.OrdinalIgnoreCase)
                    && _activeVote.ExpiresAt > Time.unscaledTime)
                {
                    startedNewSession = false;
                    Plugin.LogDebug(string.Format("Vote already active for {0}. Agree count is {1}.", _activeVote.Boss.DisplayName, _activeVote.AgreeingPlayers.Count));
                    return false;
                }

                _activeVote = new VoteSession(boss, expiresAt);
                startedNewSession = true;
                Plugin.LogInfo(string.Format("Started vote for boss {0}. Timeout in {1} seconds.", boss.DisplayName, Plugin.VoteTimeoutSeconds.Value));
                return true;
            }
        }

        public AgreementResult RegisterAgreement(string playerId, string playerName)
        {
            lock (_sync)
            {
                if (_activeVote == null)
                {
                    return AgreementResult.NoActiveVote();
                }

                if (_activeVote.ExpiresAt <= Time.unscaledTime)
                {
                    BossProgressHelper.BossContext expiredBoss = _activeVote.Boss;
                    _activeVote = null;
                    Plugin.LogInfo(string.Format("Vote for {0} expired while processing /agree from {1}.", expiredBoss.DisplayName, playerName));
                    return AgreementResult.Expired(expiredBoss);
                }

                if (!_activeVote.AgreeingPlayers.Add(playerId))
                {
                    return AgreementResult.Duplicate(_activeVote.Boss, _activeVote.AgreeingPlayers.Count, Math.Max(1, Plugin.MinAgreesRequired.Value));
                }

                int requiredAgrees = Math.Max(1, Plugin.MinAgreesRequired.Value);
                int currentAgrees = _activeVote.AgreeingPlayers.Count;
                Plugin.LogInfo(string.Format("{0} agreed for {1}. Agree count {2}/{3}.", playerName, _activeVote.Boss.DisplayName, currentAgrees, requiredAgrees));

                if (currentAgrees >= requiredAgrees)
                {
                    BossProgressHelper.BossContext approvedBoss = _activeVote.Boss;
                    _pendingApproval = approvedBoss;
                    _activeVote = null;
                    Plugin.LogInfo(string.Format("Vote approved for boss {0}. Next summon attempt is unlocked once.", approvedBoss.DisplayName));
                    return AgreementResult.Approved(approvedBoss, currentAgrees, requiredAgrees);
                }

                return AgreementResult.Added(_activeVote.Boss, currentAgrees, requiredAgrees);
            }
        }

        public bool TryConsumeApproval(BossProgressHelper.BossContext boss)
        {
            lock (_sync)
            {
                if (_pendingApproval == null)
                {
                    return false;
                }

                if (!string.Equals(_pendingApproval.ApprovalKey, boss.ApprovalKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Plugin.LogInfo(string.Format("Consumed pending approval for boss {0}.", boss.DisplayName));
                _pendingApproval = null;
                return true;
            }
        }

        public void ClearApproval(BossProgressHelper.BossContext boss)
        {
            lock (_sync)
            {
                if (_pendingApproval != null
                    && string.Equals(_pendingApproval.ApprovalKey, boss.ApprovalKey, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.LogDebug(string.Format("Clearing stale approval for boss {0}.", boss.DisplayName));
                    _pendingApproval = null;
                }
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _activeVote = null;
                _pendingApproval = null;
            }
        }

        public void Update()
        {
            BossProgressHelper.BossContext expiredBoss = null;

            lock (_sync)
            {
                if (_activeVote != null && _activeVote.ExpiresAt <= Time.unscaledTime)
                {
                    expiredBoss = _activeVote.Boss;
                    _activeVote = null;
                }
            }

            if (expiredBoss != null)
            {
                Plugin.LogInfo(string.Format("Vote expired for boss {0}.", expiredBoss.DisplayName));
                Plugin.Broadcast("Vote expired.");
            }
        }

        private sealed class VoteSession
        {
            public VoteSession(BossProgressHelper.BossContext boss, float expiresAt)
            {
                Boss = boss;
                ExpiresAt = expiresAt;
                AgreeingPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public BossProgressHelper.BossContext Boss { get; private set; }
            public float ExpiresAt { get; private set; }
            public HashSet<string> AgreeingPlayers { get; private set; }
        }

        internal sealed class AgreementResult
        {
            private AgreementResult(AgreementStatus status, BossProgressHelper.BossContext boss, int agreeCount, int requiredAgrees)
            {
                Status = status;
                Boss = boss;
                AgreeCount = agreeCount;
                RequiredAgrees = requiredAgrees;
            }

            public AgreementStatus Status { get; private set; }
            public BossProgressHelper.BossContext Boss { get; private set; }
            public int AgreeCount { get; private set; }
            public int RequiredAgrees { get; private set; }

            public static AgreementResult NoActiveVote()
            {
                return new AgreementResult(AgreementStatus.NoActiveVote, null, 0, 0);
            }

            public static AgreementResult Expired(BossProgressHelper.BossContext boss)
            {
                return new AgreementResult(AgreementStatus.Expired, boss, 0, 0);
            }

            public static AgreementResult Duplicate(BossProgressHelper.BossContext boss, int agreeCount, int requiredAgrees)
            {
                return new AgreementResult(AgreementStatus.Duplicate, boss, agreeCount, requiredAgrees);
            }

            public static AgreementResult Added(BossProgressHelper.BossContext boss, int agreeCount, int requiredAgrees)
            {
                return new AgreementResult(AgreementStatus.Added, boss, agreeCount, requiredAgrees);
            }

            public static AgreementResult Approved(BossProgressHelper.BossContext boss, int agreeCount, int requiredAgrees)
            {
                return new AgreementResult(AgreementStatus.Approved, boss, agreeCount, requiredAgrees);
            }
        }

        internal enum AgreementStatus
        {
            NoActiveVote,
            Expired,
            Duplicate,
            Added,
            Approved
        }
    }
}
