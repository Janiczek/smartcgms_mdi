using GeneticSharp;

namespace mdi_simulator
{
    internal class SearchHelpers
    {
        /* Let's cap the insulin injection amount around 40.
         * We might need to make the maximum higher for the basal insulin.
         */
        public const uint minAmount = 0;
        public const uint maxAmount = 40;
        /* <0,...>, we're trying to minimize this one.
         * Usable for BruteforceSearch as is, but for GeneticSearch we need to negate it first.
         */
        public static double Fitness(Simulation.Input input, List<Simulation.Intake> boluses)
        {
            var inputWithBoluses = input.WithBoluses(boluses);

            var output = Simulation.Simulate(inputWithBoluses, 3);

            var timeHypo = output.Count(r => r.bloodGlucose < 4);
            var timeHyper = output.Count(r => r.bloodGlucose >= 10);

            var fitness = timeHypo + timeHyper * 0.8 + boluses.Select((i) => i.amount).Sum();
            return fitness;
        }
    }
    internal class GeneticSearch
    {
        private static List<Simulation.Intake> BolusesFromChromosome(Simulation.Input input, FloatingPointChromosome c)
        {
            var values = c.ToFloatingPoints();

            var boluses = input.bolusInsulins.Select((intake, index) => new Simulation.Intake(
                intake.type,
                intake.timeMinutes,
                values[index]
            )).ToList();

            return boluses;
        }
        public static List<Simulation.Intake> FindBetterBoluses(Simulation.Input input)
        {
            Dictionary<IChromosome,double> fitnessCache = new();

            var selection = new EliteSelection();
            var crossover = new UniformCrossover();
            var mutation = new FlipBitMutation();
            var fitness = new FuncFitness((c) =>
            {
                if (fitnessCache.ContainsKey(c)) return fitnessCache[c];

                var fc = c as FloatingPointChromosome;
                var boluses = BolusesFromChromosome(input, fc!);
                var fitness = -SearchHelpers.Fitness(input, boluses);

                fitnessCache.Add(c, fitness);
                return fitness;
            });

            var bolusesCount = input.bolusInsulins.Count;

            var chromosome = new FloatingPointChromosome(
                Enumerable.Repeat((double)SearchHelpers.minAmount, bolusesCount).ToArray(),
                Enumerable.Repeat((double)SearchHelpers.maxAmount, bolusesCount).ToArray(),
                Enumerable.Repeat(6, bolusesCount).ToArray(),
                Enumerable.Repeat(0, bolusesCount).ToArray()
            );
            var population = new Population(50, 100, chromosome);

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
            ga.Termination = new FitnessStagnationTermination(100);

            ga.Start();

            var fc = ga.BestChromosome as FloatingPointChromosome;
            var boluses = BolusesFromChromosome(input, fc!);
            return boluses;
        }
    }
    internal class BruteforceSearch
    {
        public static List<Simulation.Intake> FindBetterBoluses(Simulation.Input input)
        {
            // TODO: this has hardcoded 3 bolus intakes as a nested for-loop.
            // Some other approach would be needed (streaming cartesian product?)
            // for a more general solution.

            var bolusesCount = input.bolusInsulins.Count;

            var minFitness = SearchHelpers.Fitness(input, input.bolusInsulins);
            var minBoluses = input.bolusInsulins;
            var min = Tuple.Create(minFitness, minBoluses);

            // TODO: this parallel loop results in a lot of disk thrashing -
            // SmartCGMS is trying to write into the same file from all the
            // various threads. We should parameterize the testlog.txt string.
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i0) =>
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i1) =>
            Parallel.For(SearchHelpers.minAmount, SearchHelpers.maxAmount, (i2) =>
            {
                List<uint> amounts = [(uint)i0, (uint)i1, (uint)i2];
                var newBoluses = input.bolusInsulins.Select((intake, index) => new Simulation.Intake(
                    intake.type,
                    intake.timeMinutes,
                    amounts[index]
                )).ToList();

                var fitness = SearchHelpers.Fitness(input, newBoluses);
                if (fitness < min.Item1)
                {
                    Interlocked.Exchange(ref min, Tuple.Create(fitness, newBoluses));
                }
            })));

            return min.Item2;
        }
    }
}
