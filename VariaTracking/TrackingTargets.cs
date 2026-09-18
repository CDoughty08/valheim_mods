using System;
using System.Collections.Generic;
using UnityEngine;

namespace VariaTracking
{
    internal struct TrackedTarget
    {
        public Character Character;
        public MarkerKind Kind;
        public bool Starred;
        public float DistSqr;
        public Vector3 WorldPos;
        public int InstanceId;
    }

    /// <summary>Revalidate discovered candidates each frame, including while the map is hidden.</summary>
    internal sealed class TrackingTargets
    {
        private static readonly Comparison<TrackedTarget> NearestFirst = (a, b) =>
        {
            int distance = a.DistSqr.CompareTo(b.DistSqr);
            return distance != 0 ? distance : a.InstanceId.CompareTo(b.InstanceId);
        };

        public readonly List<TrackedTarget> Items = new(64);
        public int StarredCount { get; private set; }

        /// <summary>Keep the nearest visible K in O(N log K), then order only those K.</summary>
        internal static void KeepNearest(List<TrackedTarget> visible, int count)
        {
            count = Math.Max(0, Math.Min(count, visible.Count));
            if (count == 0) { visible.Clear(); return; }
            // Max heap: the worst selected target is the cheapest candidate to replace.
            for (int i = count / 2 - 1; i >= 0; i--) SiftDown(visible, i, count);
            for (int i = count; i < visible.Count; i++)
            {
                if (NearestFirst(visible[i], visible[0]) >= 0) continue;
                visible[0] = visible[i];
                SiftDown(visible, 0, count);
            }
            if (visible.Count > count) visible.RemoveRange(count, visible.Count - count);
            visible.Sort(NearestFirst);
        }

        private static void SiftDown(List<TrackedTarget> heap, int index, int count)
        {
            while (index * 2 + 1 < count)
            {
                int child = index * 2 + 1;
                if (child + 1 < count && NearestFirst(heap[child + 1], heap[child]) > 0) child++;
                if (NearestFirst(heap[index], heap[child]) >= 0) return;
                TrackedTarget swap = heap[index]; heap[index] = heap[child]; heap[child] = swap;
                index = child;
            }
        }

        public void Clear()
        {
            Items.Clear();
            StarredCount = 0;
        }

        public void Update(IReadOnlyList<Character> candidates, Player player, Minimap minimap,
            TrackingConfigSnapshot cfg, TrackingUnlockState unlocks, float radius)
        {
            Clear();
            if (radius <= 0f || player == null)
            {
                return;
            }

            Vector3 origin = player.transform.position;
            float radiusSqr = radius * radius;
            for (int i = 0; i < candidates.Count; i++)
            {
                Character character = candidates[i];
                if (character == null)
                {
                    continue;
                }

                Vector3 position = character.transform.position;
                Vector3 delta = position - origin;
                if (delta.sqrMagnitude > radiusSqr
                    || (!unlocks.PierceFog && minimap != null && !minimap.IsExplored(position))
                    || !CreatureClassifier.TryClassify(character, player, cfg, unlocks, out MarkerKind kind, out bool starred))
                {
                    continue;
                }

                if (starred)
                {
                    StarredCount++;
                }
                Items.Add(new TrackedTarget
                {
                    Character = character,
                    Kind = kind,
                    Starred = starred,
                    WorldPos = position,
                    DistSqr = delta.x * delta.x + delta.z * delta.z,
                    InstanceId = character.GetInstanceID()
                });
            }

        }
    }
}
