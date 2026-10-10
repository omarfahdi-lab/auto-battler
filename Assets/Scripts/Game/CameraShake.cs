using UnityEngine;

namespace AutoBattler.Game
{
    /// <summary>Decaying random camera offset for impactful moments; the camera always returns to its rest position.</summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private Vector3 _rest;
        private float _strength;
        private float _duration;
        private float _remaining;

        public void SetRestPosition(Vector3 position)
        {
            _rest = position;
            transform.position = position;
        }

        /// <summary>Starts a shake unless a stronger one is already running.</summary>
        public void Shake(float strength, float duration)
        {
            float current = _remaining > 0f ? _strength * (_remaining / _duration) : 0f;
            if (strength < current) return;
            _strength = strength;
            _duration = Mathf.Max(duration, 0.01f);
            _remaining = _duration;
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f) return;
            // Unscaled so a shake feels the same at 1x and 2x battle speed.
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                transform.position = _rest;
                return;
            }
            Vector2 offset = Random.insideUnitCircle * (_strength * (_remaining / _duration));
            transform.position = _rest + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
