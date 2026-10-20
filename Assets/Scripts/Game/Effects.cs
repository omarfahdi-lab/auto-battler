using UnityEngine;

namespace AutoBattler.Game
{
    // arrow that follows its target and lands after a fixed time
    public sealed class ArrowView : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _lastTargetPos;
        private Vector3 _start;
        private float _duration;
        private float _elapsed;

        public static void Launch(Transform parent, Sprite sprite, Vector3 from, Transform target, float seconds)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5000;
            var arrow = go.AddComponent<ArrowView>();
            arrow._target = target;
            arrow._lastTargetPos = target.position + ArrowHeight;
            arrow._start = from;
            arrow._duration = Mathf.Max(seconds, 0.05f);
            arrow.Orient(arrow._lastTargetPos - from);
        }

        public static readonly Vector3 ArrowHeight = new Vector3(0f, 0.35f, 0f);

        private void Orient(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (_target != null) _lastTargetPos = _target.position + ArrowHeight;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            var previous = transform.position;
            transform.position = Vector3.Lerp(_start, _lastTargetPos, t);
            Orient(transform.position - previous);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    public static class OneShotEffect
    {
        public static void Play(Transform parent, SpriteStrip strip, Vector3 position, int sortingOrder)
        {
            var go = new GameObject("Effect");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            var animator = go.AddComponent<SpriteAnimator>();
            animator.Init(sr);
            animator.Play(strip, loop: false, onComplete: () => Object.Destroy(go));
        }
    }
}
