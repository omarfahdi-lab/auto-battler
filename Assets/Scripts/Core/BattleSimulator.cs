using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Core
{
    /// <summary>
    /// Deterministic tick-based battle. Each tick, every living combatant (in Id order) targets the nearest enemy,
    /// attacks if it is in range and off cooldown, or otherwise steps one cell toward it.
    /// </summary>
    public sealed class BattleSimulator
    {
        public const int MaxTicks = 400;

        private static readonly GridPos[] Directions =
        {
            new GridPos(0, 1), new GridPos(0, -1), new GridPos(1, 0), new GridPos(-1, 0),
            new GridPos(1, 1), new GridPos(-1, 1), new GridPos(1, -1), new GridPos(-1, -1),
        };

        private readonly List<Combatant> _combatants;
        private readonly Combatant[,] _grid = new Combatant[BoardLayout.Width, BoardLayout.Height];

        public IReadOnlyList<Combatant> Combatants => _combatants;
        public int Tick { get; private set; }
        public bool IsOver { get; private set; }
        /// <summary>Winning team, or null if the battle timed out.</summary>
        public Team? Winner { get; private set; }

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

            foreach (var c in _combatants)
            {
                if (!c.IsAlive) continue;
                if (c.Cooldown > 0) c.Cooldown--;

                var target = FindTarget(c);
                if (target == null) break;

                if (c.Position.DistanceTo(target.Position) <= c.Range)
                {
                    if (c.Cooldown == 0) AttackTarget(c, target, events);
                }
                else
                {
                    StepToward(c, target, events);
                }
            }

            Tick++;
            CheckOver();
            return events;
        }

        /// <summary>Runs until the battle ends and returns the winner (null on timeout).</summary>
        public Team? RunToEnd()
        {
            while (!IsOver) Step();
            return Winner;
        }

        private Combatant FindTarget(Combatant self)
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

        private void AttackTarget(Combatant attacker, Combatant target, List<BattleEvent> events)
        {
            int damage = Math.Min(attacker.Attack, target.Hp);
            target.Hp -= damage;
            attacker.Cooldown = attacker.Unit.Definition.AttackCooldown;
            events.Add(BattleEvent.Attack(attacker.Id, target.Id, damage));

            if (!target.IsAlive)
            {
                _grid[target.Position.X, target.Position.Y] = null;
                events.Add(BattleEvent.Death(target.Id));
            }
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

            // Only move when it actually closes the gap; blocked units wait for the path to clear.
            if (!best.HasValue || bestDistance >= currentDistance) return;

            var to = best.Value;
            _grid[from.X, from.Y] = null;
            _grid[to.X, to.Y] = mover;
            mover.Position = to;
            events.Add(BattleEvent.Move(mover.Id, from, to));
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
