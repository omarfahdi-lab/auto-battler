using System.Collections;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AutoBattler.Game
{
    /// <summary>An animated Tiny Swords unit with a star row and HP bar.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        // Characters stand a little below the centre of their frames; lift them so their feet sit in the cell.
        private const float BodyOffsetY = 0.2f;
        private const float BarY = 0.95f;
        private const float BarWidth = 0.9f;
        private const float FillInsetPixels = 8f;
        private const float FillHeightPixels = 3f;

        private static readonly Color PlayerHp = new Color(0.35f, 0.85f, 0.35f);
        private static readonly Color EnemyHp = new Color(0.95f, 0.25f, 0.25f);
        private static readonly Color StarColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color StarOutline = new Color(0.25f, 0.15f, 0.05f);
        // Hits flicker the sprite rather than tint it, so faction colours stay readable.
        private static readonly Color HitFlicker = new Color(1f, 1f, 1f, 0.35f);

        private UnitArt _art;
        private SpriteRenderer _body;
        private SpriteAnimator _animator;
        private SortingGroup _sorting;
        private Transform _hpFill;
        private float _fillWidth;
        private Vector3 _target;
        private float _speed;
        private float _hitTimer;
        private bool _dying;

        /// <summary>Bosses are drawn larger so they read as the centrepiece of the wave.</summary>
        private const float BossScale = 1.35f;

        private float _baseScale = 1f;

        public static UnitView Create(Transform parent, OwnedUnit unit, Team team, Vector3 position, bool isBoss = false)
        {
            var go = new GameObject($"{team} {unit}{(isBoss ? " (boss)" : "")}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var view = go.AddComponent<UnitView>();
            view.Build(unit, team);
            view._target = position;
            view._baseScale = isBoss ? BossScale : 1f;
            go.transform.localScale = Vector3.one * view._baseScale;
            return view;
        }

        private void Build(OwnedUnit unit, Team team)
        {
            _art = Art.Unit(unit.Definition, team);
            _sorting = gameObject.AddComponent<SortingGroup>();

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, BodyOffsetY, 0f);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.flipX = team == Team.Enemy; // sheets face right; enemies start facing the player
            _animator = bodyGo.AddComponent<SpriteAnimator>();
            _animator.Init(_body);
            _animator.Play(_art.Idle, loop: true);

            BuildHpBar(team);
            BuildStars(unit.Stars);
            if (unit.Item != null)
            {
                // Item badge to the left of the HP bar.
                SpriteFactory.CreateChild(transform, "Item", Art.IconSprite(Art.IconFor(unit.Item)), Color.white, 14,
                    new Vector3(-BarWidth / 2f - 0.12f, BarY, 0f), Vector2.one * 0.42f);
            }
            UpdateSorting();
        }

        private void BuildHpBar(Team team)
        {
            var bar = new GameObject("HpBar").transform;
            bar.SetParent(transform, false);
            bar.localPosition = new Vector3(0f, BarY, 0f);

            var frame = bar.gameObject.AddComponent<SpriteRenderer>();
            frame.sprite = Art.BarBase().ToSprite(Art.PixelsPerUnit);
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.size = new Vector2(BarWidth, frame.sprite.rect.height / Art.PixelsPerUnit);
            frame.sortingOrder = 10;

            _fillWidth = BarWidth - 2f * FillInsetPixels / Art.PixelsPerUnit;
            _hpFill = SpriteFactory.CreateChild(bar, "Fill", SpriteFactory.Square,
                team == Team.Player ? PlayerHp : EnemyHp, 11, Vector3.zero,
                new Vector2(_fillWidth, FillHeightPixels / Art.PixelsPerUnit)).transform;
        }

        private void BuildStars(int stars)
        {
            for (int i = 0; i < stars; i++)
            {
                var pos = new Vector3((i - (stars - 1) / 2f) * 0.2f, BarY + 0.2f, 0f);
                SpriteFactory.CreateChild(transform, "StarOutline", SpriteFactory.Diamond, StarOutline, 12, pos,
                    Vector2.one * 0.2f);
                SpriteFactory.CreateChild(transform, "Star", SpriteFactory.Diamond, StarColor, 13, pos,
                    Vector2.one * 0.14f);
            }
        }

        public void SetHealth(int hp, int maxHp)
        {
            float t = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            _hpFill.localScale = new Vector3(_fillWidth * t, _hpFill.localScale.y, 1f);
            _hpFill.localPosition = new Vector3(-_fillWidth * (1f - t) / 2f, 0f, 0f);
        }

        public void SnapTo(Vector3 position)
        {
            _target = position;
            transform.position = position;
            UpdateSorting();
        }

        /// <summary>Walks to a position over the given time, playing the run animation.</summary>
        public void MoveTo(Vector3 position, float seconds)
        {
            Face(position.x - transform.position.x);
            _speed = Vector3.Distance(transform.position, position) / Mathf.Max(seconds, 0.01f);
            _target = position;
            if (!_animator.IsPlayingOneShot) _animator.Play(_art.Run, loop: true);
        }

        /// <summary>Plays the attack/heal animation from frame 0, facing the target.</summary>
        public void PlayAction(Vector3 targetPosition)
        {
            var delta = targetPosition - transform.position;
            Face(delta.x);
            var strip = _art.DirectionalActions != null ? DirectionalStrip(delta) : _art.Action;
            _animator.Play(strip, loop: false, onComplete: ResumeLoop);
        }

        private SpriteStrip DirectionalStrip(Vector3 delta)
        {
            // Up, UpRight, Right, DownRight, Down; left-facing directions reuse these flipped.
            float angle = Mathf.Atan2(delta.y, Mathf.Abs(delta.x)) * Mathf.Rad2Deg;
            int index = angle > 67.5f ? 0 : angle > 22.5f ? 1 : angle > -22.5f ? 2 : angle > -67.5f ? 3 : 4;
            return _art.DirectionalActions[index];
        }

        private void ResumeLoop()
        {
            bool moving = (transform.position - _target).sqrMagnitude > 0.0001f;
            _animator.Play(moving ? _art.Run : _art.Idle, loop: true);
        }

        private void Face(float dx)
        {
            if (Mathf.Abs(dx) > 0.01f) _body.flipX = dx < 0f;
        }

        public void Hit() => _hitTimer = 0.12f;

        public void SetSelected(bool selected) => transform.localScale = Vector3.one * _baseScale * (selected ? 1.12f : 1f);

        public void Die()
        {
            if (_dying) return;
            _dying = true;
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                foreach (var r in renderers)
                {
                    var c = r.color;
                    c.a = 1f - t / 0.3f;
                    r.color = c;
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void UpdateSorting()
        {
            // Lower on screen draws in front; offset keeps every unit above the terrain (order 0-2).
            _sorting.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private void Update()
        {
            if (transform.position != _target)
            {
                transform.position = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
                UpdateSorting();
                if (transform.position == _target && !_animator.IsPlayingOneShot) _animator.Play(_art.Idle, loop: true);
            }

            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                if (!_dying) _body.color = _hitTimer > 0f ? HitFlicker : Color.white;
            }
        }
    }
}
