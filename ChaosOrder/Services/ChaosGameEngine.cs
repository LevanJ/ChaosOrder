using System.Windows;
using ChaosOrder.Models;

namespace ChaosOrder.Services
{
    // Generalized chaos-game: repeatedly jump the current point some fraction of the way
    // toward a randomly chosen target point. Which target pool (corners / side
    // midpoints / 1-of-n side points) and which fraction is used is picked at random
    // each step, weighted across the enabled rules.
    public static class ChaosGameEngine
    {
        public static List<Point> Run(
            IReadOnlyList<Point> corners,
            Point start,
            IReadOnlyList<DirectionRule> rules,
            int iterations)
        {
            var results = new List<Point>(Math.Max(0, iterations));
            if (corners.Count < 2 || iterations <= 0)
                return results;

            var effective = new List<(DirectionRule Rule, List<Point> Candidates)>();
            foreach (var rule in rules)
            {
                if (!rule.IsEnabled || rule.Weight <= 0 || double.IsNaN(rule.Ratio) || rule.Ratio == 0)
                    continue;

                var candidates = GetCandidates(corners, rule);
                if (candidates.Count > 0)
                    effective.Add((rule, candidates));
            }

            if (effective.Count == 0)
                return results;

            double totalWeight = effective.Sum(e => e.Rule.Weight);
            var rng = new Random();
            var current = start;

            for (int i = 0; i < iterations; i++)
            {
                double roll = rng.NextDouble() * totalWeight;
                var chosen = effective[^1];
                double acc = 0;
                foreach (var e in effective)
                {
                    acc += e.Rule.Weight;
                    if (roll <= acc)
                    {
                        chosen = e;
                        break;
                    }
                }

                var target = chosen.Candidates[rng.Next(chosen.Candidates.Count)];
                double t = chosen.Rule.Ratio;
                current = new Point(
                    current.X + (target.X - current.X) * t,
                    current.Y + (target.Y - current.Y) * t);
                results.Add(current);
            }

            return results;
        }

        private static List<Point> GetCandidates(IReadOnlyList<Point> corners, DirectionRule rule)
        {
            var list = new List<Point>();
            switch (rule.TargetType)
            {
                case DirectionTargetType.Corners:
                    list.AddRange(corners);
                    break;
                case DirectionTargetType.MiddleOfSides:
                    AddSidePoints(corners, list, 2);
                    break;
                case DirectionTargetType.NthPointOfSides:
                    AddSidePoints(corners, list, Math.Max(1, rule.N));
                    break;
            }
            return list;
        }

        // For each side (corner[i] -> corner[i+1]), the point at 1/n of the way along it.
        // n = 2 gives the midpoint.
        private static void AddSidePoints(IReadOnlyList<Point> corners, List<Point> list, int n)
        {
            int count = corners.Count;
            double t = 1.0 / n;
            for (int i = 0; i < count; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % count];
                list.Add(new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
        }
    }
}
