using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Core
{
    // tick based + deterministic. every tick each unit (in id order) either finishes its wind-up,
    // starts an attack if something's in range, or moves a cell closer
    public sealed class BattleSimulator
    {
        // 10 ticks/sec = the 10fps animations, so 1 tick = 1 frame
        public const int TicksPerSecond = 10;
        public const int MaxTicks = 120 * TicksPerSecond;
        public const int ProjectileTicksPerCell = 1;

        private static readonly GridPos[] Directions =
        {
            new GridPos(0, 1), new GridPos(0, -1), new GridPos(1, 0), new GridPos(-1, 0),
            new GridPos(1, 1), new GridPos(-1, 1), new GridPos(1, -1), new GridPos(-1, -1),
        };

        private struct Projectile
        {
            public int SourceId;
            public int TargetId;
            public int Damage;
            public int ArriveTick;
        }

        private readonly List<Combatant> _combatants;
        private readonly Combatant[,] _grid = new Combatant[BoardLayout.Width, BoardLayout.Height];
        private readonly List<Projectile> _projectiles = new List<Projectile>();

        public IReadOnlyList<Combatant> Combatants => _combatants;
        public int Tick { get; private set; }
        public bool IsOver { get; private set; }
        // null = timed out
        public Team? Winner { get; private set; }
        public int ProjectilesInFlight => _projectiles.Count;

        public BattleSimulator(IEnumerable<Combatant> combatants)
        {
            _combatants = combatants.OrderBy(c => c.Id).ToList();
            foreach (var c in _combatants)
            {
                if (!BoardLayout.InBounds(c.Position))
                    throw new ArgumentException($"Combatant {c.Id} is out of bounds at {c.Position}");
                if (_grid[c.Position.X, c.Position.Y] != null)
                    throw new ArgumentException($"Two combatants share cell {c.Position}");
                _grid[c.Position.X, c.Position.Y] = c;
            }
            CheckOver();
        }

        public int AliveCount(Team team) => _combatants.Count(c => c.IsAlive && c.Team == team);

        public Combatant Find(int id) => _combatants.FirstOrDefault(c => c.Id == id);

        public IReadOnlyList<BattleEvent> Step()
        {
            var events = new List<BattleEvent>();
            if (IsOver) return events;

            LandProjectiles(events);

            foreach (var c in _combatants)
            {
                if (!c.IsAlive) continue;
                if (c.Cooldown > 0) c.Cooldown--;
                if (c.MoveCooldown > 0) c.MoveCooldown--;

                if (c.IsActing)
                {
                    if (Tick >= c.PendingImpactTick) ResolveAction(c, events);
                    continue;
                }

                if (c.Definition.Role == UnitRole.Healer) ActAsHealer(c, events);
                else ActAsFighter(c, events);
            }

            Tick++;
            CheckOver();
            return events;
        }

        public Team? RunToEnd()
        {
            while (!IsOver) Step();
            return Winner;
        }

        private void ActAsFighter(Combatant c, List<BattleEvent> events)
        {
            var target = NearestEnemy(c);
            if (target == null) return;

            if (c.Position.DistanceTo(target.Position) <= c.Range)
            {
                if (c.Cooldown == 0) StartAction(c, target, events);
            }
            else if (c.MoveCooldown == 0)
            {
                StepToward(c, target, events);
            }
        }

        private void ActAsHealer(Combatant c, List<BattleEvent> events)
        {
            var patient = MostInjuredAlly(c);
            if (patient != null)
            {
                if (c.Position.DistanceTo(patient.Position) <= c.Range)
                {
                    if (c.Cooldown == 0) StartAction(c, patient, events);
                }
                else if (c.MoveCooldown == 0)
                {
                    StepToward(c, patient, events);
                }
                return;
            }

            // nobody's hurt, just follow the fight but stay at range
            var enemy = NearestEnemy(c);
            if (enemy != null && c.MoveCooldown == 0 && c.Position.DistanceTo(enemy.Position) > c.Range)
                StepToward(c, enemy, events);
        }

        private void StartAction(Combatant c, Combatant target, List<BattleEvent> events)
        {
            c.Cooldown = c.Stats.ActionCooldown;
            c.PendingTargetId = target.Id;
            c.PendingImpactTick = Tick + c.Definition.ImpactTicks;
            events.Add(BattleEvent.ActionStart(c.Id, target.Id, c.Position, target.Position));
            if (c.Definition.ImpactTicks == 0) ResolveAction(c, events);
        }

        private void ResolveAction(Combatant c, List<BattleEvent> events)
        {
            var target = Find(c.PendingTargetId);
            c.PendingTargetId = -1;
            // target died during the wind-up, whiff
            if (target == null || !target.IsAlive) return;

            if (c.Definition.Role == UnitRole.Healer)
            {
                int amount = Math.Min(c.Power, target.MaxHp - target.Hp);
                target.Hp += amount;
                events.Add(BattleEvent.Heal(c.Id, target.Id, amount));
            }
            else if (c.Definition.UsesProjectile)
            {
                int travel = Math.Max(1, c.Position.DistanceTo(target.Position) * ProjectileTicksPerCell);
                _projectiles.Add(new Projectile
                {
                    SourceId = c.Id, TargetId = target.Id, Damage = c.Power, ArriveTick = Tick + travel,
                });
                events.Add(BattleEvent.ProjectileLaunch(c.Id, target.Id, c.Position, target.Position, travel));
            }
            else
            {
                ApplyDamage(c.Id, target, c.Power, events);
            }
        }

        private void LandProjectiles(List<BattleEvent> events)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                var p = _projectiles[i];
                if (p.ArriveTick > Tick) continue;
                _projectiles.RemoveAt(i--);
                var target = Find(p.TargetId);
                if (target != null && target.IsAlive) ApplyDamage(p.SourceId, target, p.Damage, events);
            }
        }

        // always at least 1 dmg
        public static int Mitigate(int power, int armorPct) => Math.Max(1, power * (100 - armorPct) / 100);

        private void ApplyDamage(int sourceId, Combatant target, int power, List<BattleEvent> events)
        {
            int damage = Math.Min(Mitigate(power, target.Stats.ArmorPct), target.Hp);
            target.Hp -= damage;
            events.Add(BattleEvent.Damage(sourceId, target.Id, damage));

            if (!target.IsAlive)
            {
                target.PendingTargetId = -1;
                _grid[target.Position.X, target.Position.Y] = null;
                events.Add(BattleEvent.Death(target.Id));
            }
        }

        private Combatant NearestEnemy(Combatant self)
        {
            Combatant best = null;
            int bestDistance = int.MaxValue;
            foreach (var other in _combatants)
            {
                if (!other.IsAlive || other.Team == self.Team) continue;
                int d = self.Position.DistanceTo(other.Position);
                if (d < bestDistance)
                {
                    best = other;
                    bestDistance = d;
                }
            }
            return best;
        }

        // lowest hp % ally (can be itself), ties -> lower id
        private Combatant MostInjuredAlly(Combatant self)
        {
            Combatant best = null;
            foreach (var other in _combatants)
            {
                if (!other.IsAlive || other.Team != self.Team || other.Hp >= other.MaxHp) continue;
                // cross multiply instead of using floats
                if (best == null || (long)other.Hp * best.MaxHp < (long)best.Hp * other.MaxHp) best = other;
            }
            return best;
        }

        private void StepToward(Combatant mover, Combatant target, List<BattleEvent> events)
        {
            var from = mover.Position;
            int currentDistance = from.DistanceTo(target.Position);
            GridPos? best = null;
            int bestDistance = currentDistance;
            int bestManhattan = int.MaxValue;

            foreach (var dir in Directions)
            {
                var candidate = new GridPos(from.X + dir.X, from.Y + dir.Y);
                if (!BoardLayout.InBounds(candidate) || _grid[candidate.X, candidate.Y] != null) continue;

                int d = candidate.DistanceTo(target.Position);
                int m = candidate.ManhattanTo(target.Position);
                if (d < bestDistance || (d == bestDistance && best.HasValue && m < bestManhattan))
                {
                    best = candidate;
                    bestDistance = d;
                    bestManhattan = m;
                }
            }

            // only move if it actually gets closer, otherwise wait for the path to open up
            if (!best.HasValue || bestDistance >= currentDistance) return;

            var to = best.Value;
            _grid[from.X, from.Y] = null;
            _grid[to.X, to.Y] = mover;
            mover.Position = to;
            mover.MoveCooldown = mover.Definition.MoveTicks;
            events.Add(BattleEvent.Move(mover.Id, from, to, mover.Definition.MoveTicks));
        }

        private void CheckOver()
        {
            int players = AliveCount(Team.Player);
            int enemies = AliveCount(Team.Enemy);
            if (players == 0 || enemies == 0)
            {
                IsOver = true;
                Winner = players > 0 ? Team.Player : enemies > 0 ? Team.Enemy : (Team?)null;
            }
            else if (Tick >= MaxTicks)
            {
                IsOver = true;
                Winner = null;
            }
        }
    }
}
