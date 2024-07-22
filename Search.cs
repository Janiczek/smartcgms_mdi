using GeneticSharp;
using System.Diagnostics.CodeAnalysis;

namespace mdi_simulator
{
    internal class SearchHelpers
    {
        /* Let's cap the insulin injection amount around 40.
         * We might need to make the maximum higher for the basal insulin.
         */
        public const uint minAmount = 0;
        public const uint maxAmount = 40;
        /* 0..1, we're trying to minimize this one.
         * Usable for BruteforceSearch as is, but for GeneticSearch we need to negate it first.
         */
        public static double Fitness(Simulation.Input input, Simulation.Intake basal, List<Simulation.Intake> boluses)
        {
            var updatedInput = input.WithBoluses(boluses).WithBasal(basal);

            var dayMinutes = 24 * 60;
            var output = Simulation.Simulate(updatedInput, 3);
            var lastDay = output.Skip(output.Count - dayMinutes); // Let's check the day where the insulin has stabilized

            var hypoCount = lastDay.Count(r => r.bloodGlucose < 4);                  // 0..output.Count
            var hyperCount = lastDay.Count(r => r.bloodGlucose >= 10);               // 0..output.Count
            var totalInsulin = basal.amount + boluses.Select((i) => i.amount).Sum(); // minAmount..maxAmount*boluses.Count

            // TODO also check hypo/hyper for the skipped days, but with less weight

            var amplitude = lastDay.Max(r => r.bloodGlucose) - lastDay.Min(r => r.bloodGlucose); // ideally around 3-4, 6+ is bad

            var hypoWeight = 1.0;
            var hyperWeight = 0.8;
            var totalInsulinWeight = 0.3;
            var amplitudeWeight = 0.5;

            var hypoNormalized = hypoCount / (double)dayMinutes;                           // 0..1
            var hyperNormalized = hyperCount / (double)dayMinutes;                         // 0..1
            var totalInsulinNormalized = totalInsulin / (maxAmount * (1 + boluses.Count)); // 0..1
            var amplitudeNormalized = amplitude >= 6 ? 1 : amplitude / 6;                  // 0..1

            var weights = hypoWeight + hyperWeight + totalInsulinWeight + amplitudeWeight;

            return (hypoNormalized * hypoWeight
                + hyperNormalized * hyperWeight
                + totalInsulinNormalized * totalInsulinWeight
                + amplitudeNormalized * amplitudeWeight) / weights; // 0..1
        }
    }
    internal class GeneticSearch
    {
        private static List<Simulation.Intake> IntakeAmountsFromChromosome(Simulation.Input input, FloatingPointChromosome c)
        {
            var values = c.ToFloatingPoints();

            var basal = input.basalInsulin.WithAmount(values[0]);
            var boluses = input.bolusInsulins.Select((intake, index) => intake.WithAmount(values[index + 1])).ToList();

            return [basal, .. boluses];
        }
        public static Simulation.Input FindBetterInput(Simulation.Input input)
        {
            Dictionary<List<Simulation.Intake>, double> fitnessCache = new(new BolusesSameAmount());

            var selection = new EliteSelection();
            var crossover = new UniformCrossover();
            var mutation = new FlipBitMutation();
            var fitness = new FuncFitness((c) =>
            {
                var fc = c as FloatingPointChromosome;
                var amounts = IntakeAmountsFromChromosome(input, fc!);

                if (fitnessCache.TryGetValue(amounts, out var cachedFitness))
                    return cachedFitness;

                var basal = amounts[0];
                var boluses = amounts.Skip(1).ToList();
                var fitness = -SearchHelpers.Fitness(input, basal, boluses);

                fitnessCache.Add(amounts, fitness);
                return fitness;
            });

            var bolusesCount = input.bolusInsulins.Count;
            var amountsCount = 1 + bolusesCount; // basal

            var chromosome = new FloatingPointChromosome(
                Enumerable.Repeat((double)SearchHelpers.minAmount, amountsCount).ToArray(),
                Enumerable.Repeat((double)SearchHelpers.maxAmount, amountsCount).ToArray(),
                Enumerable.Repeat(6, amountsCount).ToArray(),
                Enumerable.Repeat(0, amountsCount).ToArray()
            );
            var population = new Population(50, 100, chromosome);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
            ga.Termination = new FitnessStagnationTermination(100);

            ga.Start();

            var fc = ga.BestChromosome as FloatingPointChromosome;
            var intakes = IntakeAmountsFromChromosome(input, fc!);
            return input.WithBasal(intakes[0]).WithBoluses(intakes.Skip(1).ToList());
        }
    }
    internal class BolusesSameAmount : EqualityComparer<List<Simulation.Intake>>
    {
        public override bool Equals(List<Simulation.Intake>? x, List<Simulation.Intake>? y)
        {
            return x.Zip(y, (a, b) => a.amount == b.amount).All(b => b);
        }
        public override int GetHashCode([DisallowNull] List<Simulation.Intake> obj)
        {
            return obj.Select(obj => obj.amount).Aggregate(0, (a, b) => a ^ b.GetHashCode()).GetHashCode();
        }
    }
    internal class BruteforceSearch
    {
        public static Simulation.Input FindBetterInput(Simulation.Input input)
        {
            // TODO: this has hardcoded 3 bolus intakes as a nested for-loop.
            // Some other approach would be needed (streaming cartesian product?)
            // for a more general solution.

            var bolusesCount = input.bolusInsulins.Count;

            var minFitness = SearchHelpers.Fitness(input, input.basalInsulin, input.bolusInsulins);
            var minBasal = input.basalInsulin;
            var minBoluses = input.bolusInsulins;
            var min = Tuple.Create(minFitness, minBasal, minBoluses);

            // TODO: this parallel loop results in a lot of disk thrashing -
            // SmartCGMS is trying to write into the same file from all the
            // various threads. We should parameterize the testlog.txt string.
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (newBasalAmount) =>
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i0) =>
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i1) =>
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i2) =>
            {
                List<uint> amounts = [(uint)i0, (uint)i1, (uint)i2];
                var newBasal = input.basalInsulin.WithAmount(newBasalAmount);
                var newBoluses = input.bolusInsulins.Select((intake, index) => intake.WithAmount(amounts[index])).ToList();

                var fitness = SearchHelpers.Fitness(input, newBasal, newBoluses);
                if (fitness < min.Item1)
                {
                    Interlocked.Exchange(ref min, Tuple.Create(fitness, newBasal, newBoluses));
                }
            }))));

            return input.WithBasal(min.Item2).WithBoluses(min.Item3);
        }
    }
}
