using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemShareFix.Core
{
    /// <summary>
    /// Owns ItemShareFix claim transitions and historical barriers. The ledger separates current-generation state from
    /// stable-user history so collection, deferred grants, and confirmed disconnect cancellation cannot be granted twice.
    /// </summary>
    public sealed class ClaimLedger
    {
        private readonly Dictionary<ClaimKey, ClaimRecord> _records = new Dictionary<ClaimKey, ClaimRecord>();
        private readonly Dictionary<HistoricalClaimKey, HistoricalClaimRecord> _history = new Dictionary<HistoricalClaimKey, HistoricalClaimRecord>();

        public IReadOnlyCollection<ClaimRecord> Records => _records.Values;
        public IReadOnlyCollection<HistoricalClaimRecord> HistoricalRecords => _history.Values;

        public ClaimRecord Ensure(SharedPickupKey pickup, ParticipantKey participant, ParticipantState state, int stage)
        {
            if (!TryEnsure(pickup, participant, state, stage, out var record))
                throw new InvalidOperationException("Historical pickup barrier rejected participant generation: " + pickup + "/" + participant);
            return record;
        }

        public bool TryEnsure(SharedPickupKey pickup, ParticipantKey participant, ParticipantState state, int stage, out ClaimRecord record)
        {
            var key = new ClaimKey(pickup, participant);
            if (_records.TryGetValue(key, out var existing))
            {
                record = existing;
                return true;
            }

            if (IsHistoricallyBlocked(pickup, participant.StableUser))
            {
                record = null!;
                return false;
            }

            var initial = state == ParticipantState.FullyDead ? ClaimState.Deferred : ClaimState.Pending;
            if (state == ParticipantState.Disconnected) initial = ClaimState.CancelledDisconnected;

            var created = new ClaimRecord(key, initial, stage);
            if (initial == ClaimState.CancelledDisconnected)
            {
                created.TerminalStage = stage;
                SetHistory(pickup, participant.StableUser, HistoricalClaimState.CancelledDisconnected, stage);
            }
            _records.Add(key, created);
            record = created;
            return true;
        }

        public bool TryGet(SharedPickupKey pickup, ParticipantKey participant, out ClaimRecord record)
            => _records.TryGetValue(new ClaimKey(pickup, participant), out record!);

        public bool IsHistoricallyBlocked(SharedPickupKey pickup, StableUserKey stableUser)
            => _history.ContainsKey(new HistoricalClaimKey(pickup, stableUser));

        public bool TryGetHistorical(SharedPickupKey pickup, StableUserKey stableUser, out HistoricalClaimRecord record)
            => _history.TryGetValue(new HistoricalClaimKey(pickup, stableUser), out record!);

        public IReadOnlyList<HistoricalClaimRecord> HistoricalFor(StableUserKey stableUser)
            => _history.Values
                .Where(x => x.Key.StableUser.Equals(stableUser))
                .OrderBy(x => x.Key.Pickup.Value)
                .ToArray();

        public bool MarkCollected(SharedPickupKey pickup, ParticipantKey participant, int stage)
        {
            if (!TryGet(pickup, participant, out var record)) return false;
            if (record.State != ClaimState.Pending) return false;
            record.State = ClaimState.Collected;
            record.TerminalStage = stage;
            SetHistory(pickup, participant.StableUser, HistoricalClaimState.Collected, stage);
            return true;
        }

        public int TransitionParticipant(ParticipantKey participant, ParticipantState from, ParticipantState to, int stage)
        {
            _ = from;
            var changed = 0;
            foreach (var record in _records.Values.Where(x => x.Key.Participant.Equals(participant)).ToArray())
            {
                if (to == ParticipantState.Disconnected)
                {
                    if (record.State == ClaimState.Pending || record.State == ClaimState.Deferred)
                    {
                        record.State = ClaimState.CancelledDisconnected;
                        record.TerminalStage = stage;
                        SetHistory(record.Key.Pickup, participant.StableUser, HistoricalClaimState.CancelledDisconnected, stage);
                        changed++;
                    }
                    continue;
                }

                if (to == ParticipantState.FullyDead && record.State == ClaimState.Pending)
                {
                    record.State = ClaimState.Deferred;
                    changed++;
                }
            }
            return changed;
        }

        public bool MarkDeferredGranted(ClaimKey key, int stage)
        {
            if (!_records.TryGetValue(key, out var record)) return false;
            if (record.State != ClaimState.Deferred) return false;
            record.State = ClaimState.GrantedDeferred;
            record.TerminalStage = stage;
            SetHistory(record.Key.Pickup, record.Key.Participant.StableUser, HistoricalClaimState.GrantedDeferred, stage);
            return true;
        }

        public IReadOnlyList<ClaimRecord> DeferredFor(ParticipantKey participant, int beforeStage)
            => _records.Values
                .Where(x => x.Key.Participant.Equals(participant) && x.State == ClaimState.Deferred && x.CreatedStage < beforeStage)
                .OrderBy(x => x.CreatedStage)
                .ThenBy(x => x.Key.Pickup.Value)
                .ToArray();

        public int TransferPickup(SharedPickupKey oldPickup, SharedPickupKey newPickup)
        {
            if (oldPickup.Equals(newPickup)) return 0;
            var movingRecords = _records.Values.Where(x => x.Key.Pickup.Equals(oldPickup)).ToArray();
            var movingHistory = _history.Values.Where(x => x.Key.Pickup.Equals(oldPickup)).ToArray();

            foreach (var record in movingRecords)
            {
                var newKey = new ClaimKey(newPickup, record.Key.Participant);
                if (_records.ContainsKey(newKey))
                    throw new InvalidOperationException("Transfer would create duplicate pickup/participant state: " + newKey);
            }

            foreach (var historical in movingHistory)
            {
                var newKey = new HistoricalClaimKey(newPickup, historical.Key.StableUser);
                if (_history.TryGetValue(newKey, out var existing) && existing.State != historical.State)
                    throw new InvalidOperationException("Transfer would create conflicting historical pickup/stable-user state: " + newKey);
            }

            foreach (var record in movingRecords)
            {
                var oldKey = record.Key;
                var newKey = new ClaimKey(newPickup, oldKey.Participant);
                _records.Remove(oldKey);
                record.Key = newKey;
                _records.Add(newKey, record);
            }

            foreach (var historical in movingHistory)
            {
                var oldKey = historical.Key;
                var newKey = new HistoricalClaimKey(newPickup, oldKey.StableUser);
                _history.Remove(oldKey);
                if (_history.TryGetValue(newKey, out var existing))
                {
                    if (historical.Stage > existing.Stage) existing.Stage = historical.Stage;
                    continue;
                }
                historical.Key = newKey;
                _history.Add(newKey, historical);
            }

            return movingRecords.Length;
        }

        public int OnStageTransition(int newStage)
        {
            _ = newStage;
            var removed = 0;
            foreach (var pair in _records.ToArray())
            {
                var state = pair.Value.State;
                if (state == ClaimState.Pending || state == ClaimState.Collected || state == ClaimState.CancelledDisconnected || state == ClaimState.GrantedDeferred)
                {
                    _records.Remove(pair.Key);
                    removed++;
                }
            }
            _history.Clear();
            return removed;
        }

        public int Clear()
        {
            var count = _records.Count + _history.Count;
            _records.Clear();
            _history.Clear();
            return count;
        }

        public int RemoveTerminalOlderThan(int stageExclusive)
        {
            var removed = 0;
            foreach (var pair in _records.ToArray())
            {
                var terminalStage = pair.Value.TerminalStage;
                if (terminalStage.HasValue && terminalStage.Value < stageExclusive)
                {
                    _records.Remove(pair.Key);
                    removed++;
                }
            }
            foreach (var pair in _history.ToArray())
            {
                if (pair.Value.Stage < stageExclusive)
                {
                    _history.Remove(pair.Key);
                    removed++;
                }
            }
            return removed;
        }

        private void SetHistory(SharedPickupKey pickup, StableUserKey stableUser, HistoricalClaimState state, int stage)
        {
            var key = new HistoricalClaimKey(pickup, stableUser);
            if (_history.TryGetValue(key, out var existing))
            {
                if (existing.State != state)
                {
                    // Collected / granted are stronger than disconnect cancellation; never downgrade them.
                    if (existing.State == HistoricalClaimState.Collected || existing.State == HistoricalClaimState.GrantedDeferred) return;
                    existing.State = state;
                }
                if (stage > existing.Stage) existing.Stage = stage;
                return;
            }
            _history.Add(key, new HistoricalClaimRecord(key, state, stage));
        }
    }
}
