using AutoBattler.Core;
using UnityEngine;

namespace AutoBattler.Game
{
    /// <summary>Placeholder visual for a unit: team ring, coloured body, star pips and an HP bar.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        private const float MoveSpeed = 8f;
        private const float BarWidth = 0.8f;

        private static readonly Color PlayerRing = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color EnemyRing = new Color(0.9f, 0.2f, 0.25f);
        private static readonly Color HpColor = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color HpBack = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color StarColor = new Color(1f, 0.85f, 0.2f);

        private SpriteRenderer _body;
        private SpriteRenderer _ring;
        private Transform _hpFill;
        private Vector3 _target;
        private float _flash;
        private Color _bodyColor;

        public static UnitView Create(Transform parent, OwnedUnit unit, Team team, Vector3 position)
        {
            var go = new GameObject(unit.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var view = go.AddComponent<UnitView>();
            view.Build(unit, team);
            view._target = position;
            return view;
        }

        private void Build(OwnedUnit unit, Team team)
        {
            _ring = SpriteFactory.CreateChild(transform, "Ring", SpriteFactory.Circle,
                team == Team.Player ? PlayerRing : EnemyRing, 10, Vector3.zero, Vector2.one * 0.8f);
            _bodyColor = SpriteFactory.FromRgb(unit.Definition.Color);
            _body = SpriteFactory.CreateChild(transform, "Body", SpriteFactory.Circle, _bodyColor, 11,
                Vector3.zero, Vector2.one * 0.66f);

            // Ranged units get a square marker so silhouettes differ at a glance.
            if (unit.Definition.Range > 1)
                SpriteFactory.CreateChild(transform, "RangedMark", SpriteFactory.Square, new Color(0, 0, 0, 0.35f), 12,
                    Vector3.zero, Vector2.one * 0.2f);

            for (int i = 0; i < unit.Stars; i++)
            {
                float x = (i - (unit.Stars - 1) / 2f) * 0.18f;
                SpriteFactory.CreateChild(transform, "Star", SpriteFactory.Square, StarColor, 13,
                    new Vector3(x, 0.44f, 0), Vector2.one * 0.12f);
            }

            SpriteFactory.CreateChild(transform, "HpBack", SpriteFactory.Square, HpBack, 13,
                new Vector3(0, -0.46f, 0), new Vector2(BarWidth, 0.08f));
            _hpFill = SpriteFactory.CreateChild(transform, "HpFill", SpriteFactory.Square, HpColor, 14,
                new Vector3(0, -0.46f, 0), new Vector2(BarWidth, 0.08f)).transform;
        }

        public void MoveTo(Vector3 position) => _target = position;

        public void SnapTo(Vector3 position)
        {
            _target = position;
            transform.position = position;
        }

        public void SetHealth(int hp, int maxHp)
        {
            float t = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            _hpFill.localScale = new Vector3(BarWidth * t, _hpFill.localScale.y, 1f);
            _hpFill.localPosition = new Vector3(-BarWidth * (1f - t) / 2f, _hpFill.localPosition.y, 0f);
        }

        public void Flash() => _flash = 0.15f;

        public void SetSelected(bool selected) => transform.localScale = Vector3.one * (selected ? 1.15f : 1f);

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _target, MoveSpeed * Time.deltaTime);
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                _body.color = _flash > 0f ? Color.white : _bodyColor;
            }
        }
    }
}
