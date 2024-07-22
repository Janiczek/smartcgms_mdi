using System.Text;

namespace mdi_simulator
{
    internal class Simulation
    {
        public enum IntakeType
        {
            BasalInsulin,
            BolusInsulin,
            Carbs,
        }
        public readonly struct Intake(IntakeType type,
                                      uint timeMinutes,
                                      double amount) : IComparable<Intake>
        {
            public readonly IntakeType type = type;
            public readonly uint timeMinutes = timeMinutes;
            public readonly double amount = amount;

            // Used for easier addition to PriorityQueue
            public int CompareTo(Intake other) => timeMinutes.CompareTo(other.timeMinutes);

            public override string ToString()
            {
                string unit = type switch
                {
                    IntakeType.BasalInsulin => "U",
                    IntakeType.BolusInsulin => "U",
                    IntakeType.Carbs => "g",
                    _ => throw new NotImplementedException("wut"),
                };

                string hh = (timeMinutes / 60).ToString().PadLeft(2, '0');
                string mm = (timeMinutes % 60).ToString().PadLeft(2, '0');

                return $"{hh}:{mm} - {amount} {unit}";
            }
        }
        public readonly struct Input(Intake basalInsulin,
                                     List<Intake> bolusInsulins,
                                     List<Intake> carbs)
        {
            public readonly Intake basalInsulin = basalInsulin;
            public readonly List<Intake> bolusInsulins = bolusInsulins;
            public readonly List<Intake> carbs = carbs;

            public Input WithBoluses(List<Intake> newBoluses) => new(basalInsulin, newBoluses, carbs);

            public List<Intake> ToList() => new List<Intake> { basalInsulin }.Concat(bolusInsulins).Concat(carbs).ToList();

            public PriorityQueue<Intake, Intake> ToPriorityQueue()
            {
                PriorityQueue<Intake, Intake> queue = new();
                ToList().ForEach(i => queue.Enqueue(i, i));
                return queue;
            }

            public override string ToString()
            {
                StringBuilder sb = new();
                sb.AppendLine($"Basal insulin: {basalInsulin}");
                bolusInsulins.ForEach(i => sb.AppendLine($"Bolus insulin: {i}"));
                carbs.ForEach(i => sb.AppendLine($"Carbs: {i}"));
                return sb.ToString();
            }
        }
        public readonly struct OutputRow(double minute,
                                         double bloodGlucose,
                                         double carbohydratesOnBoard,
                                         double insulinOnBoard,
                                         double interstitialGlucose)
        {
            public readonly double minute = minute;
            public readonly double bloodGlucose = bloodGlucose;
            public readonly double carbohydratesOnBoard = carbohydratesOnBoard;
            public readonly double insulinOnBoard = insulinOnBoard;
            public readonly double interstitialGlucose = interstitialGlucose;

            public override string ToString() => $"{((uint)(minute / 60)).ToString().PadLeft(2, '0')}:{(minute % 60).ToString().PadLeft(2, '0')},{bloodGlucose},{carbohydratesOnBoard},{insulinOnBoard}";
        }

        public static List<OutputRow> Simulate(Input input, uint days)
        {
            string logFile = "testlog.txt";
            List<OutputRow> output = [];
            SCGMS_Game game = new(1, 1, 60 * 1000, logFile);

            for (uint day = 0; day < days; day++)
            {
                uint minsCurrent = 0;
                uint minsTarget = 24 * 60;
                uint today = day * minsTarget;

                var queue = input.ToPriorityQueue();

                while (queue.Count > 0)
                {
                    var nextIntake = queue.Dequeue();
                    for (uint minsLeft = nextIntake.timeMinutes - minsCurrent; minsLeft > 0; minsLeft--)
                    {
                        game.Step();
                        output.Add(new(today + minsCurrent, game.BloodGlucose, game.CarbohydratesOnBoard, game.InsulinOnBoard, game.InterstitialGlucose));
                        minsCurrent++;
                    }

                    switch (nextIntake.type)
                    {
                        case IntakeType.BasalInsulin:
                            // TODO: later when the basal insulin is available in the model
                            // as an one-time injection, use _that_ instead of Schedule..Rate
                            game.ScheduleInsulinBasalRate(nextIntake.amount / 24, 0);
                            break;
                        case IntakeType.BolusInsulin:
                            game.ScheduleInsulinBolus(nextIntake.amount, 0);
                            break;
                        case IntakeType.Carbs:
                            game.ScheduleCarbohydratesIntake(nextIntake.amount, 1);
                            break;
                    }
                }

                // Simulate the rest of the day
                for (uint minsLeft = minsTarget - minsCurrent; minsLeft > 0; minsLeft--)
                {
                    game.Step();
                    output.Add(new(today + minsCurrent, game.BloodGlucose, game.CarbohydratesOnBoard, game.InsulinOnBoard, game.InterstitialGlucose));
                    minsCurrent++;
                }

            }

            game.Terminate();

            return output;
        }

        private const uint Hour = 60;

        public static Input ExampleInput = new(
            new(IntakeType.BasalInsulin, 22 * Hour, 32),
            [
                new(IntakeType.BolusInsulin, 10 * Hour, 18),
                new(IntakeType.BolusInsulin, 13 * Hour, 22),
                new(IntakeType.BolusInsulin, 19 * Hour, 21),
            ],
            [
                new(IntakeType.Carbs, 10 * Hour, 24),
                new(IntakeType.Carbs, 13 * Hour, 60),
                new(IntakeType.Carbs, 19 * Hour, 60),
                new(IntakeType.Carbs, 22 * Hour, 24),
            ]
        );
    }
}
