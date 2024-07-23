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
        public GeneticAlgorithm ga;
        private Simulation.Input input;

        public GeneticSearch(Simulation.Input input_)
        {
            input = input_;

            Dictionary<List<Simulation.Intake>, double> fitnessCache = new(new IntakesSameAmount());

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

            ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
            ga.Termination = new FitnessStagnationTermination(100);
        }

        private static List<Simulation.Intake> IntakeAmountsFromChromosome(Simulation.Input input, FloatingPointChromosome c)
        {
            var values = c.ToFloatingPoints();

            var basal = input.basalInsulin.WithAmount(values[0]);
            var boluses = input.bolusInsulins.Select((intake, index) => intake.WithAmount(values[index + 1])).ToList();

            return [basal, .. boluses];
        }
        public static Simulation.Input InputFromChromosome(Simulation.Input input, IChromosome c)
        {
            var fc = c as FloatingPointChromosome;
            var intakes = IntakeAmountsFromChromosome(input, fc!);
            return input.WithBasal(intakes[0]).WithBoluses(intakes.Skip(1).ToList());
        }
        public Simulation.Input FindBetterInput()
        {
            ga.Start();
            return InputFromChromosome(input, ga.BestChromosome);
        }
    }
    internal class IntakesSameAmount : EqualityComparer<List<Simulation.Intake>>
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
}
