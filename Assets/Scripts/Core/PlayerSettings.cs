using System;

namespace AutoBattler.Core
{
    /// <summary>Player preferences: master volume (with mute) and battle speed. Persisted as a short string.</summary>
    public sealed class PlayerSettings
    {
        /// <summary>Volume levels the volume button cycles through, in percent. The last one is mute.</summary>
        public static readonly int[] VolumeSteps = { 100, 60, 30, 0 };
        public const int MaxBattleSpeed = 2;

        private int _lastAudibleStep;

        public int VolumeStep { get; private set; }
        public int VolumePercent => VolumeSteps[VolumeStep];
        public bool IsMuted => VolumePercent == 0;
        /// <summary>1 for normal speed, 2 for double speed.</summary>
        public int BattleSpeed { get; private set; } = 1;

        public void CycleVolume()
        {
            VolumeStep = (VolumeStep + 1) % VolumeSteps.Length;
            if (!IsMuted) _lastAudibleStep = VolumeStep;
        }

        /// <summary>Mutes, or restores the last audible volume.</summary>
        public void ToggleMute()
        {
            if (IsMuted)
            {
                VolumeStep = _lastAudibleStep;
            }
            else
            {
                _lastAudibleStep = VolumeStep;
                VolumeStep = VolumeSteps.Length - 1;
            }
        }

        public void ToggleBattleSpeed() => BattleSpeed = BattleSpeed == 1 ? MaxBattleSpeed : 1;

        public string Serialize() => $"{VolumeStep},{_lastAudibleStep},{BattleSpeed}";

        /// <summary>Restores saved settings; anything missing or invalid falls back to the defaults.</summary>
        public static PlayerSettings Deserialize(string text)
        {
            var settings = new PlayerSettings();
            var parts = (text ?? "").Split(',');
            if (parts.Length != 3) return settings;
            if (!int.TryParse(parts[0], out int step) || !int.TryParse(parts[1], out int audible) ||
                !int.TryParse(parts[2], out int speed)) return settings;

            if (step >= 0 && step < VolumeSteps.Length) settings.VolumeStep = step;
            if (audible >= 0 && audible < VolumeSteps.Length - 1) settings._lastAudibleStep = audible;
            if (speed == 1 || speed == MaxBattleSpeed) settings.BattleSpeed = speed;
            return settings;
        }
    }

    public static class SoundVariation
    {
        /// <summary>A playback pitch within +/- <paramref name="spreadPercent"/> of 1, so repeated sounds differ.</summary>
        public static float Pitch(IRandom rng, int spreadPercent)
        {
            if (spreadPercent <= 0) return 1f;
            return 1f + (rng.Next(2 * spreadPercent + 1) - spreadPercent) / 100f;
        }
    }
}
