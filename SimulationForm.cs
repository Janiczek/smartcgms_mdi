using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace mdi_simulator
{
    public partial class SimulationForm : Form
    {
        public SimulationForm()
        {
            InitializeComponent();
        }

        private static PlotModel MakeModel(Simulation.Input input)
        {
            var model = new PlotModel { Title = "Simulation Results" };

            model.Axes.Add(new OxyPlot.Axes.LinearAxis { 
                Position = OxyPlot.Axes.AxisPosition.Bottom, 
                Title = "Time", 
                LabelFormatter = TimeLabelFormatter,
                Minimum = 0,
                Maximum = 24 * 60,
            });
            model.Axes.Add(new OxyPlot.Axes.LinearAxis { 
                Position = OxyPlot.Axes.AxisPosition.Left, 
                Title = "Blood glucose [mmol/l]",
                Minimum = 0,
                MaximumPadding = 0.1,
            });

            var output = Simulation.Simulate(input);

            var seriesBloodGlucose = new LineSeries { 
                Title = "Blood Glucose",
            };

            output.ForEach(row => seriesBloodGlucose.Points.Add(new DataPoint(row.minute, row.bloodGlucose)));

            model.Series.Add(seriesBloodGlucose);

            return model;
        }

        private static string TimeLabelFormatter(double mins)
        {
            uint h = (uint)(mins / 60);
            uint m = (uint)(mins % 60);
            return $"{h.ToString().PadLeft(2,'0')}:{m.ToString().PadLeft(2,'0')}";
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            var plot = new PlotView();
            plot.Dock = DockStyle.Fill;
            plot.Location = new Point(0, 0);
            plot.Name = "BG Plot";
            plot.PanCursor = Cursors.Hand;
            plot.Size = new Size(484, 312);
            plot.TabIndex = 0;
            plot.Text = "BG Plot";
            plot.ZoomHorizontalCursor = Cursors.SizeWE;
            plot.ZoomRectangleCursor = Cursors.SizeNWSE;
            plot.ZoomVerticalCursor = Cursors.SizeNS;

            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(484, 312);
            Controls.Add(plot);
            Name = "MDI Simulator";
            Text = "MDI Simulator";
            ResumeLayout(false);

            var model = MakeModel(Simulation.ExampleInput);
            plot.Model = model;
        }
    }
}