using System;

namespace GameDummyTest
{
    class Program
    {
        static void Main(string[] args)
        {
            SCGMS_Game game = new SCGMS_Game(1, 1, 60000, "testlog.txt");

            for (int i = 0; i < 10; i++)
            {
                game.Step();
                Console.WriteLine(game.BloodGlucose.ToString());
            }

            game.ScheduleInsulinBolus(8.0, 0.5);

            for (int i = 0; i < 50; i++)
            {
                game.Step();
                Console.WriteLine(game.BloodGlucose.ToString());
            }

            game.ScheduleCarbohydratesIntake(35.0, 0.25);

            for (int i = 0; i < 50; i++)
            {
                game.Step();
                Console.WriteLine(game.BloodGlucose.ToString());
            }

            game.Terminate();
        }
    }
}
