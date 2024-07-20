using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace mdi_simulator
{
    public partial class SimulationForm : Form
    {
        private PlotView plot;

        public SimulationForm()
        {
            InitializeComponent();
            plot!.Model = MakeModel(Simulation.ExampleInput);
        }


        private const uint days = 5;
        private static PlotModel MakeModel(Simulation.Input input)
        {
            var model = new PlotModel { Title = "Simulation Results" };

            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Time",
                LabelFormatter = TimeLabelFormatter,
                Minimum = 0,
                Maximum = days * 24 * 60,
                MajorStep = 6 * 60,
                MinorStep = 6 * 60,
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Blood glucose [mmol/l]",
                Minimum = 0,
                MaximumPadding = 0.1,
            });

            // Add vertical line annotations for each day's midnight
            for (int i = 1; i <= days; i++)
            {
                model.Annotations.Add(new LineAnnotation {
                    Type = LineAnnotationType.Vertical,
                    X = i * 24 * 60,
                    Color = OxyColors.Black,
                    StrokeThickness = 1,
                });
            }

            // Delimit the hypo- and hyperglycemia ranges (4-10 mmol/l)
            model.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = 0,
                MaximumX = days * 24 * 60,
                MinimumY = 10,
                MaximumY = 20,
                Fill = OxyColor.FromAColor(30, OxyColors.Red),
                StrokeThickness = 0,
            });
            model.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = 0,
                MaximumX = days * 24 * 60,
                MinimumY = 0,
                MaximumY = 4,
                Fill = OxyColor.FromAColor(50, OxyColors.Red),
                StrokeThickness = 0,
            });

            var output = Simulation.Simulate(input, days);

            var seriesBloodGlucose = new LineSeries
            {
                Title = "Blood Glucose",
            };

            output.ForEach(row => seriesBloodGlucose.Points.Add(new DataPoint(row.minute, row.bloodGlucose)));

            model.Series.Add(seriesBloodGlucose);

            return model;
        }

        private static string TimeLabelFormatter(double mins)
        {
            uint h = (uint)(mins % (24 * 60) / 60);
            return $"{h}h";
        }

        private void InitializeComponent()
        {
            plot = new PlotView();
            SuspendLayout();

            plot.Dock = DockStyle.Fill;
            plot.Location = new Point(0, 0);
            plot.Margin = new Padding(4, 3, 4, 3);
            plot.Name = "plot";
            plot.PanCursor = Cursors.Hand;
            plot.Size = new Size(920, 360);
            plot.TabIndex = 0;
            plot.Text = "BG Plot";
            plot.ZoomHorizontalCursor = Cursors.SizeWE;
            plot.ZoomRectangleCursor = Cursors.SizeNWSE;
            plot.ZoomVerticalCursor = Cursors.SizeNS;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(920, 360);
            Controls.Add(plot);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MDI Simulator";
            Text = "MDI Simulator";

            ResumeLayout(false);
        }
    }
}