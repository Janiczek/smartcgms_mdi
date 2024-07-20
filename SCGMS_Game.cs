using System.Runtime.InteropServices;

namespace GameDummyTest
{
    public partial class SCGMS_Game
    {
        [LibraryImport("game-wrapper", EntryPoint = "scgms_game_create")]
        private static partial IntPtr Create(UInt16 configClass, UInt16 configId, UInt32 steppingMs, IntPtr logFilePath);

        [LibraryImport("game-wrapper", EntryPoint = "scgms_game_step")]
        private static partial Int32 Step(IntPtr game, Guid[] inputSignalIds, double[] inputSignalLevels, double[] inputSignalTimes, UInt32 inputSignalCount, out double bg, out double ig, out double iob, out double cob);

        [LibraryImport("game-wrapper", EntryPoint = "scgms_game_terminate")]
        private static partial Int32 Terminate(IntPtr game);

        // requested insulin basal rate signal ID
        public static readonly Guid signal_Requested_Insulin_Basal_Rate = new("B5897BBD-1E32-408A-A0D5-C5BFECF447D9");
        // requested insulin bolus signal ID
        public static readonly Guid signal_Requested_Insulin_Bolus = new("09B16B4A-54C2-4C6A-948A-3DEF8533059B");
        // requested carbohydrates intake signal ID
        public static readonly Guid signal_Carb_Intake = new("37AA6AC1-6984-4A06-92CC-A660110D0DC7");
        // requested rescue carbohydrates intake signal ID
        public static readonly Guid signal_Carb_Rescue = new("F24920F7-3F7B-4000-B2D0-374F940E4898");
        // requested physical activity signal ID
        public static readonly Guid signal_Physical_Activity = new("F4438E9A-DD52-45BD-83CE-5E93615E62BD");

        // instance created with Create call (scgms_game_create)
        private IntPtr GameInstance;

        public double BloodGlucose { get; private set; }
        public double InterstitialGlucose { get; private set; }
        public double InsulinOnBoard { get; private set; }
        public double CarbohydratesOnBoard { get; private set; }

        private List<Guid> InputIds = new();
        private List<double> InputLevels = new();
        private List<double> InputTimes = new();

        public SCGMS_Game(UInt16 configClass, UInt16 configId, UInt32 steppingMs, String logFilePath)
        {
            IntPtr stringPtr = Marshal.StringToHGlobalAnsi(logFilePath);
            GameInstance = Create(configClass, configId, steppingMs, stringPtr);
            Marshal.FreeHGlobal(stringPtr);

            if (GameInstance == IntPtr.Zero)
               throw new Exception("Could not create game instance");
        }

        /// <summary>
        /// Schedules a level event to be sent to a SCGMS backend
        /// </summary>
        /// <param name="id">signal ID</param>
        /// <param name="level">signal level</param>
        /// <param name="time">signal time (relative factor of step size, <0;1) )</param>
        public void ScheduleSignalLevel(Guid id, double level, double time)
        {
            InputIds.Add(id);
            InputLevels.Add(level);
            InputTimes.Add(time);
        }

        /// <summary>
        /// Schedules insulin bolus to be requested to a SCGMS backend
        /// </summary>
        /// <param name="level">amount to be delivered [U]</param>
        /// <param name="time">time to deliver (relative factor of step size, <0;1) )</param>
        public void ScheduleInsulinBolus(double level, double time)
        {
            ScheduleSignalLevel(signal_Requested_Insulin_Bolus, level, time);
        }

        /// <summary>
        /// Schedules insulin basal rate to be requested to a SCGMS backend
        /// </summary>
        /// <param name="level">amount to be requested [U/hr]</param>
        /// <param name="time">time to deliver (relative factor of step size, <0;1) )</param>
        public void ScheduleInsulinBasalRate(double level, double time)
        {
            ScheduleSignalLevel(signal_Requested_Insulin_Basal_Rate, level, time);
        }

        /// <summary>
        /// Schedules regular carbohydrates (CHO, meal) intake to be requested to a SCGMS backend
        /// </summary>
        /// <param name="level">amount to be delivered [g]</param>
        /// <param name="time">time to deliver (relative factor of step size, <0;1) )</param>
        public void ScheduleCarbohydratesIntake(double level, double time)
        {
            ScheduleSignalLevel(signal_Carb_Intake, level, time);
        }

        /// <summary>
        /// Schedules rescue carbohydrates (CHO) intake to be requested to a SCGMS backend
        /// </summary>
        /// <param name="level">amount to be delivered [g]</param>
        /// <param name="time">time to deliver (relative factor of step size, <0;1) )</param>
        public void ScheduleCarbohydratesRescue(double level, double time)
        {
            ScheduleSignalLevel(signal_Carb_Rescue, level, time);
        }

        /// <summary>
        /// Schedules physical activity to be sent to a SCGMS backend
        /// NOTE: there's no timer to cancel the excercise; when you want to end previously requested excercise, call this method with level = 0.0
        /// </summary>
        /// <param name="level">physical activity intensity [] (common values: 0.1 for light, 0.25 for medium and 0.4 for intensive excercise)</param>
        /// <param name="time">time to deliver (relative factor of step size, <0;1) )</param>
        public void SchedulePhysicalActivity(double level, double time)
        {
            ScheduleSignalLevel(signal_Physical_Activity, level, time);
        }

        /// <summary>
        /// Performs step in SCGMS game backend instance
        /// </summary>
        /// <returns>did the step succeed?</returns>
        public bool Step()
        {
            // cannot step on an invalid instance
            if (GameInstance == IntPtr.Zero)
                return false;

            var res = Step(GameInstance, InputIds.ToArray(), InputLevels.ToArray(), InputTimes.ToArray(), (UInt32)InputIds.Count, out double bg, out double ig, out double iob, out double cob);

            InputIds.Clear();
            InputLevels.Clear();
            InputTimes.Clear();

            BloodGlucose = bg;
            InterstitialGlucose = ig;
            InsulinOnBoard = iob;
            CarbohydratesOnBoard = cob;

            return (res > 0);
        }

        /// <summary>
        /// Terminates SCGMS backend execution; this method may block
        /// </summary>
        /// <returns>did the termination succeed?</returns>
        public bool Terminate()
        {
            // cannot terminate an empty instance
            if (GameInstance == IntPtr.Zero)
                return false;

            bool result = (Terminate(GameInstance) > 0);

            // clear the instance
            if (result)
                GameInstance = IntPtr.Zero;

            return result;
        }
    }
}
