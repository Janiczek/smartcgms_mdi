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
        public readonly struct Intake(IntakeType type, uint timeMinutes, double amount) : IComparable<Intake>
        {
            public readonly IntakeType type = type;
            public readonly uint timeMinutes = timeMinutes;
            public readonly double amount = amount;

            public int CompareTo(Intake other) => timeMinutes.CompareTo(other.timeMinutes);
        }
        public readonly struct Input(Intake basalInsulin, List<Intake> bolusInsulins, List<Intake> carbs)
        {
            public PriorityQueue<Intake, Intake> ToPriorityQueue()
            {
                PriorityQueue<Intake, Intake> queue = new();
                queue.Enqueue(basalInsulin, basalInsulin);
                bolusInsulins.ForEach(i => queue.Enqueue(i, i));
                carbs.ForEach(i => queue.Enqueue(i, i));
                return queue;
            }
        }
        public readonly struct OutputRow(double minute, double bloodGlucose, double carbohydratesOnBoard, double insulinOnBoard, double interstitialGlucose)
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

            // TODO: is the current time right after initializing the game 00:00:00 - midnight?
            // Does the model even care? (Dawn phenomenon)

            // TODO: try simulating eg. 5 days, to let the basal insulin stabilize. Toujeo docs say it does take ~5 days.
            // Or maybe somehow put the existing insulin on board into the init state of the model?
            // Don't forget to copy the intakes each day. We only have them planned for the first day in the Intake.

            for (uint day = 0; day < days; day++)
            {

                Console.WriteLine($"Day {day + 1} of {days}");

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

                    // TODO is the 0 value for the time parameter of the Schedule... methods OK?
                    switch (nextIntake.type)
                    {
                        case IntakeType.BasalInsulin:
                            // TODO: Basal _rate_? How to convert my one-time injection to a rate?
                            // It's U/hr instead of U, so I'm assuming I can divide by 24.
                            // It probably then adds an assumption that it's linear, but Toujeo isn't linear. (It tries to be though.)
                            game.ScheduleInsulinBasalRate(nextIntake.amount / 24, 0);
                            break;
                        case IntakeType.BolusInsulin:
                            game.ScheduleInsulinBolus(nextIntake.amount, 0);
                            break;
                        case IntakeType.Carbs:
                            game.ScheduleCarbohydratesIntake(nextIntake.amount, 0);
                            break;
                    }
                }

                // Finish the rest of the simulation
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
                new(IntakeType.BolusInsulin, 9 * Hour, 18),
                new(IntakeType.BolusInsulin, 13 * Hour, 22),
                new(IntakeType.BolusInsulin, 19 * Hour, 21),
            ],
            [
                new(IntakeType.Carbs, 10 * Hour, 24),
                new(IntakeType.Carbs, 12 * Hour, 12),
                new(IntakeType.Carbs, 13 * Hour, 60),
                new(IntakeType.Carbs, 19 * Hour, 60),
                new(IntakeType.Carbs, 22 * Hour, 24),
            ]
        );
    }
}
