using UnityEngine;

namespace Varia.Shared
{
    /// <summary>Qualifying movement and elapsed-time accounting shared by passive skills.</summary>
    internal sealed class MovementExperience
    {
        private Player _player;
        private float _elapsed;
        private float _experience;

        internal void Reset()
        {
            _player = null;
            _elapsed = _experience = 0f;
        }

        internal void Tick(Player player, Skills.SkillType skill, float rate, float interval,
            float minDirectionSqr, float minSpeed)
        {
            if (!ReferenceEquals(_player, player))
            {
                Reset();
                _player = player;
            }
            if (player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting())
            {
                Reset();
                return;
            }
            if (rate <= 0f || player.IsSitting() || player.IsAttached() || player.IsAttachedToShip()
                || !player.CanMove() || player.GetMoveDir().sqrMagnitude < minDirectionSqr) return;
            Vector3 velocity = player.GetVelocity();
            velocity.y = 0f;
            if (velocity.sqrMagnitude < minSpeed * minSpeed) return;

            // Integrate the current rate too: changing loads/targets cannot retroactively
            // change the value of movement accumulated earlier in the interval.
            _elapsed += Time.deltaTime;
            _experience += rate * Time.deltaTime;
            if (_elapsed < interval) return;
            float amount = _experience;
            _elapsed = _experience = 0f;
            Skills skills = player.GetSkills();
            if (skills != null && skills.GetSkill(skill).m_level < Skills.c_MaxSkillLevel)
                player.RaiseSkill(skill, amount);
        }
    }
}
