using System;
using AutoBattler.Core;
using UnityEngine;

namespace AutoBattler.Game
{
    // plays strips at 10fps (same as the sim tick rate) so frame N shows up on tick N
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

        // restarts from frame 0, unless it's the same loop that's already playing
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
                    done?.Invoke(); // usually plays the next strip
                    return;
                }
                _renderer.sprite = _strip.Frames[_frame];
            }
        }
    }
}
