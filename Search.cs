using GeneticSharp;
using System.Diagnostics.CodeAnalysis;

namespace mdi_simulator
{
    internal class SearchHelpers
    {
        public const uint minAmount = 0;
        public const uint maxAmount = 50;
        /* 0..1, we're trying to minimize this one.
         * Usable for BruteforceSearch as is, but for GeneticSearch we need to negate it first.
         */
        public static double Fitness(Simulation.Input input, Simulation.Intake basal, List<Simulation.Intake> boluses)
        {
            var updatedInput = input.WithBoluses(boluses).WithBasal(basal);

            uint days = 3;
            var dayMinutes = 24 * 60;
            var output = Simulation.Simulate(updatedInput, days);
            var initialDays = output.Take((int)(days - 1) * dayMinutes);
            var lastDay = output.Skip(output.Count - dayMinutes);

            var longtermHypoCount = lastDay.Count(r => r.bloodGlucose < 4);
            var longtermHyperCount = lastDay.Count(r => r.bloodGlucose >= 10);
            var longtermAmplitude = lastDay.Max(r => r.bloodGlucose) - lastDay.Min(r => r.bloodGlucose); // ideally around 3-4, 6+ is bad
            var totalBolusInsulin = boluses.Select((i) => i.amount).Sum(); // minAmount..maxAmount
            var basalInsulin = basal.amount; // minAmount..maxAmount
            var initialHypoCount = initialDays.Count(r => r.bloodGlucose < 4);
            var initialHyperCount = initialDays.Count(r => r.bloodGlucose >= 10);

            double longtermHypoWeight = 15;
            double longtermHyperWeight = 12;
            double longtermAmplitudeWeight = 6;
            double totalBolusInsulinWeight = 4;
            double basalInsulinWeight = 2;
            double initialHypoWeight = 2;
            double initialHyperWeight = 1;

            var longtermHypoNormalized = longtermHypoCount / (double)lastDay.Count(); // 0..1
            var longtermHyperNormalized = longtermHyperCount / (double)lastDay.Count(); // 0..1
            var longtermAmplitudeNormalized = longtermAmplitude >= 6 ? 1 : (double)(longtermAmplitude / 6); // 0..1
            var totalBolusInsulinNormalized = totalBolusInsulin / (maxAmount * boluses.Count); // 0..1
            var basalInsulinNormalized = basalInsulin / maxAmount; // 0..1
            var initialHypoNormalized = initialHypoCount / (double)initialDays.Count(); // 0..1
            var initialHyperNormalized = initialHyperCount / (double)initialDays.Count(); // 0..1

            var weights = longtermHypoWeight + longtermHyperWeight + initialHypoWeight + initialHyperWeight + longtermAmplitudeWeight + totalBolusInsulinWeight + basalInsulinWeight;

            return (longtermHypoNormalized * longtermHypoWeight
                + longtermHyperNormalized * longtermHyperWeight
                + longtermAmplitudeNormalized * longtermAmplitudeWeight
                + totalBolusInsulinNormalized * totalBolusInsulinWeight
                + basalInsulinNormalized + basalInsulinWeight
                + initialHypoNormalized * initialHypoWeight
                + initialHyperNormalized * initialHyperWeight
                ) / weights; // 0..1
            

            // Interesting values we've ended up on (2024-07-23):
            //
            // OLD FITNESS FUNCTION
            // 15 / 29,1,18
            // 16 / 28,1,19
            // 15 / 27,4,19
            // 17 / 23,6,18
            // 29 / 16,4,8 
            //
            // NEW FITNESS FUNCTION:
        }
    }
    internal class GeneticSearch
    {
        public GeneticAlgorithm ga;
        private Simulation.Input input;
        private Dictionary<List<Simulation.Intake>, double> fitnessCache = new(new IntakesSameAmount());

        public GeneticSearch(Simulation.Input input_)
        {
            input = input_;

            var selection = new EliteSelection(5);
            var crossover = new UniformCrossover();
            var mutation = new UniformMutation();
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
            var population = new Population(128, 256, chromosome);
            population.GenerationStrategy = new PerformanceGenerationStrategy();

            ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
            ga.TaskExecutor = new ParallelTaskExecutor { MinThreads = 1, MaxThreads = 8 };
            ga.Termination = new FitnessStagnationTermination(50);
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
