using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class PlayerSettingsTests
    {
        [Test]
        public void Defaults_AreFullVolumeAndNormalSpeed()
        {
            var s = new PlayerSettings();
            Assert.That(s.VolumePercent, Is.EqualTo(100));
            Assert.That(s.IsMuted, Is.False);
            Assert.That(s.BattleSpeed, Is.EqualTo(1));
        }

        [Test]
        public void CycleVolume_StepsDownToMuteThenWrapsAround()
        {
            var s = new PlayerSettings();
            var seen = Enumerable.Range(0, PlayerSettings.VolumeSteps.Length + 1).Select(_ =>
            {
                int v = s.VolumePercent;
                s.CycleVolume();
                return v;
            }).ToArray();

            Assert.That(seen, Is.EqualTo(new[] { 100, 60, 30, 0, 100 }));
        }

        [Test]
        public void ToggleMute_RestoresThePreviousVolume()
        {
            var s = new PlayerSettings();
            s.CycleVolume(); // 60%
            s.ToggleMute();
            Assert.That(s.IsMuted);
            s.ToggleMute();
            Assert.That(s.VolumePercent, Is.EqualTo(60));
        }

        [Test]
        public void BattleSpeed_TogglesBetweenOneAndTwo()
        {
            var s = new PlayerSettings();
            s.ToggleBattleSpeed();
            Assert.That(s.BattleSpeed, Is.EqualTo(2));
            s.ToggleBattleSpeed();
            Assert.That(s.BattleSpeed, Is.EqualTo(1));
        }

        [Test]
        public void Settings_RoundTripThroughSerialization()
        {
            var s = new PlayerSettings();
            s.CycleVolume();
            s.ToggleMute();
            s.ToggleBattleSpeed();

            var restored = PlayerSettings.Deserialize(s.Serialize());

            Assert.That(restored.IsMuted);
            Assert.That(restored.BattleSpeed, Is.EqualTo(2));
            restored.ToggleMute();
            Assert.That(restored.VolumePercent, Is.EqualTo(60), "unmuting remembers the old level");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("9,9,9")]
        [TestCase("1,2")]
        public void Deserialize_FallsBackToDefaultsOnBadInput(string text)
        {
            var s = PlayerSettings.Deserialize(text);
            Assert.That(s.VolumeStep, Is.InRange(0, PlayerSettings.VolumeSteps.Length - 1));
            Assert.That(s.BattleSpeed, Is.EqualTo(1).Or.EqualTo(2));
        }

        [Test]
        public void PitchVariation_StaysInRangeAndVaries()
        {
            var rng = new SeededRandom(3);
            var pitches = Enumerable.Range(0, 500).Select(_ => SoundVariation.Pitch(rng, 8)).ToList();

            Assert.That(pitches.All(p => p >= 0.92f && p <= 1.08f));
            Assert.That(pitches.Distinct().Count(), Is.GreaterThan(5));
            Assert.That(SoundVariation.Pitch(rng, 0), Is.EqualTo(1f));
        }
    }
}
