using GameDummyTest;

string logFile = "testlog.txt";

List<OutputRow> simulate(Input input)
{
    List<OutputRow> output = new();
    var queue = input.ToPriorityQueue();
    SCGMS_Game game = new(1, 1, 60*1000, logFile);

    // TODO: is the current time right after initializing the game 00:00:00 - midnight?
    // Does the model even care? (Dawn phenomenon)

    // TODO: try simulating eg. 5 days, to let the basal insulin stabilize. Toujeo docs say it does take ~5 days.
    // Or maybe somehow put the existing insulin on board into the init state of the model?
    // Don't forget to copy the intakes each day. We only have them planned for the first day in the Intake.

    uint minsCurrent = 0;
    uint minsTarget = 24 * 60;

    while (queue.Count > 0)
    {
        var nextIntake = queue.Dequeue();
        for (uint minsLeft = nextIntake.TimeMinutes - minsCurrent; minsLeft > 0; minsLeft--)
        {
            game.Step();
            output.Add(new(minsCurrent, game.BloodGlucose, game.CarbohydratesOnBoard, game.InsulinOnBoard, game.InterstitialGlucose));
            minsCurrent++;
        }

        // TODO is the 0 value for the time parameter of the Schedule... methods OK?
        switch (nextIntake.Type)
        {
            case IntakeType.BasalInsulin:
                // TODO: Basal _rate_? How to convert my one-time injection to a rate?
                // It's U/hr instead of U, so I'm assuming I can divide by 24.
                // It probably then adds an assumption that it's linear, but Toujeo isn't linear. (It tries to be though.)
                game.ScheduleInsulinBasalRate(nextIntake.Amount / 24, 0);
                break;
            case IntakeType.BolusInsulin:
                game.ScheduleInsulinBolus(nextIntake.Amount, 0);
                break;
            case IntakeType.Carbs:
                game.ScheduleCarbohydratesIntake(nextIntake.Amount, 0);
                break;
        }
    }

    // Finish the rest of the simulation
    for (uint minsLeft = minsTarget - minsCurrent; minsLeft > 0; minsLeft--)
    {
        game.Step();
        output.Add(new(minsCurrent, game.BloodGlucose, game.CarbohydratesOnBoard, game.InsulinOnBoard, game.InterstitialGlucose));
        minsCurrent++;
    }

    game.Terminate();

    return output;
}

void writeOutputToCsv(List<OutputRow> output)
{
    string filePath = "output.csv";
    using FileStream fileStream = File.Create(filePath);
    using StreamWriter writer = new(fileStream);

    writer.WriteLine("TimeOfDay,BloodGlucose,CarbohydratesOnBoard,InsulinOnBoard");
    foreach (var row in output) writer.WriteLine(row.ToSimpleString());
}

///////////////////

uint hour = 60;

Input input = new(
    new(IntakeType.BasalInsulin, 22 * hour, 32),
    new List<Intake> {
        new(IntakeType.BolusInsulin, 9 * hour, 18),
        new(IntakeType.BolusInsulin, 13 * hour, 22),
        new(IntakeType.BolusInsulin, 19 * hour, 21),
    },
    new List<Intake> {
        new(IntakeType.Carbs, 10 * hour, 24),
        new(IntakeType.Carbs, 12 * hour, 12),
        new(IntakeType.Carbs, 13 * hour, 60),
        new(IntakeType.Carbs, 19 * hour, 60),
        new(IntakeType.Carbs, 22 * hour, 24),
    }
);

List<OutputRow> output = simulate(input);
writeOutputToCsv(output);

///////////////////

enum IntakeType
{
    BasalInsulin,
    BolusInsulin,
    Carbs,
}

readonly struct Intake : IComparable<Intake>
{
    public readonly IntakeType Type;
    public readonly uint TimeMinutes;
    public readonly double Amount;

    public Intake(IntakeType type, uint timeMinutes, double amount) : this()
    {
        Type = type;
        TimeMinutes = timeMinutes;
        Amount = amount;
    }

    public int CompareTo(Intake other) => TimeMinutes.CompareTo(other.TimeMinutes);
}

readonly struct Input
{
    // TODO: basal brand = Toujeo (insulin glargine)
    // Page 16 of https://www.medsafe.govt.nz/profs/datasheet/t/toujeoinj.pdf has a graph of the GIR
    // 
    // TODO: bolus brand = Lyumjev (insulin lispro-aabc)
    // Section 12.2 of https://uspl.lilly.com/lyumjev/lyumjev.html has a graph of the GIR

    readonly Intake BasalInsulin;
    readonly List<Intake> BolusInsulins;
    readonly List<Intake> Carbs;

    public Input(Intake basalInsulin, List<Intake> bolusInsulins, List<Intake> carbs)
    {
        BasalInsulin = basalInsulin;
        BolusInsulins = bolusInsulins;
        Carbs = carbs;
    }

    public PriorityQueue<Intake, Intake> ToPriorityQueue()
    {
        PriorityQueue<Intake, Intake> queue = new();
        queue.Enqueue(BasalInsulin, BasalInsulin);
        foreach (var bolusInsulin in BolusInsulins) queue.Enqueue(bolusInsulin, bolusInsulin);
        foreach (var carb in Carbs) queue.Enqueue(carb, carb);
        return queue;
    }
}

readonly struct OutputRow
{
    readonly double Minute;
    readonly double BloodGlucose;
    readonly double CarbohydratesOnBoard;
    readonly double InsulinOnBoard;
    readonly double InterstitialGlucose;

    public OutputRow(double minute, double bloodGlucose, double carbohydratesOnBoard, double insulinOnBoard, double interstitialGlucose)
    {
        Minute = minute;
        BloodGlucose = bloodGlucose;
        CarbohydratesOnBoard = carbohydratesOnBoard;
        InsulinOnBoard = insulinOnBoard;
        InterstitialGlucose = interstitialGlucose;
    }

    public string ToSimpleString() => $"{((uint)(Minute / 60)).ToString().PadLeft(2,'0')}:{(Minute % 60).ToString().PadLeft(2,'0')},{BloodGlucose},{CarbohydratesOnBoard},{InsulinOnBoard}";
}
