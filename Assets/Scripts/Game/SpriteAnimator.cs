using System;
using AutoBattler.Core;
using UnityEngine;

namespace AutoBattler.Game
{
    /// <summary>
    /// Plays sprite strips at the battle tick rate (10 fps), so frame N of an action is on screen exactly when the
    /// simulation resolves tick N of it.
    /// </summary>
    public sealed class SpriteAnimator : MonoBehaviour
    {
        public const float FrameSeconds = 1f / BattleSimulator.TicksPerSecond;

        private SpriteRenderer _renderer;
        private SpriteStrip _strip;
        private bool _loop;
        private int _frame;
        private float _timer;
        private Action _onComplete;

        public SpriteStrip Current => _strip;
        public bool IsPlayingOneShot => _strip != null && !_loop;

        public void Init(SpriteRenderer target) => _renderer = target;

        /// <summary>Starts a strip from frame 0. Looping strips that are already playing are left alone.</summary>
        public void Play(SpriteStrip strip, bool loop, Action onComplete = null)
        {
            if (loop && _loop && strip == _strip) return;
            _strip = strip;
            _loop = loop;
            _frame = 0;
            _timer = 0f;
            _onComplete = onComplete;
            _renderer.sprite = strip.Frames[0];
        }

        private void Update()
        {
            if (_strip == null) return;
            _timer += Time.deltaTime;
            while (_timer >= FrameSeconds)
            {
                _timer -= FrameSeconds;
                if (_frame + 1 < _strip.Length)
                {
                    _frame++;
                }
                else if (_loop)
                {
                    _frame = 0;
                }
                else
                {
                    var done = _onComplete;
                    _onComplete = null;
                    _strip = null;
                    done?.Invoke(); // usually starts the next strip
                    return;
                }
                _renderer.sprite = _strip.Frames[_frame];
            }
        }
    }
}
