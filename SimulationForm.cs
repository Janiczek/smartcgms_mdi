using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace mdi_simulator
{
    public partial class SimulationForm : Form
    {
        private Simulation.Input originalInput = Simulation.ExampleInput;
        private Simulation.Input bestInput = Simulation.ExampleInput;

        public SimulationForm()
        {
            InitializeComponent();
            RefreshLayout();
        }
        
        private void RefreshLayout()
        {
            inputLabel.Text = $"Original:\n{originalInput}\nBest:\n{bestInput}";
            plot.Model = MakeModel();
        }

        private const uint days = 5;
        private const uint maxY = 50;
        private PlotModel MakeModel()
        {
            var model = new PlotModel
            {
                Title = "Simulation Results",
            };

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
                model.Annotations.Add(new LineAnnotation
                {
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
                MaximumY = maxY,
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

            var originalOutput = Simulation.Simulate(originalInput, days);
            var bestOutput = Simulation.Simulate(bestInput, days);

            var seriesOriginalGlucose = new LineSeries { Title = "Original glucose", Color = OxyColor.FromAColor(75,OxyColors.OrangeRed) };
            var seriesBestGlucose = new LineSeries { Title = "Best glucose", Color = OxyColors.Green };
            originalOutput.ForEach(row => seriesOriginalGlucose.Points.Add(new DataPoint(row.minute, row.bloodGlucose)));
            bestOutput.ForEach(row => seriesBestGlucose.Points.Add(new DataPoint(row.minute, row.bloodGlucose)));
            model.Series.Add(seriesOriginalGlucose);
            model.Series.Add(seriesBestGlucose);

            for (int i = 0; i < days; i++)
            {
                originalInput.ToList().ForEach(input =>
                {
                    var color = input.type switch
                    {
                        Simulation.IntakeType.BasalInsulin => OxyColors.Red,
                        Simulation.IntakeType.BolusInsulin => OxyColors.Green,
                        Simulation.IntakeType.Carbs => OxyColors.Blue,
                        _ => throw new NotImplementedException("wut"),
                    };

                    var textY = input.type switch
                    {
                        Simulation.IntakeType.BasalInsulin => 0.5,
                        Simulation.IntakeType.BolusInsulin => 1.25,
                        Simulation.IntakeType.Carbs => 2,
                        _ => throw new NotImplementedException("wut"),
                    };

                    var text = input.type switch
                    {
                        Simulation.IntakeType.BasalInsulin => "B",
                        Simulation.IntakeType.BolusInsulin => "I",
                        Simulation.IntakeType.Carbs => "C",
                        _ => throw new NotImplementedException("wut"),
                    };

                    var opacity = (byte)(i == 0 ? 255 : 50);
                    var colorWithOpacity = OxyColor.FromAColor(opacity, color);

                    model.Annotations.Add(new LineAnnotation
                    {
                        Type = LineAnnotationType.Vertical,
                        X = i * 24 * 60 + input.timeMinutes,
                        Color = colorWithOpacity,
                        LineStyle = LineStyle.Solid,
                        MaximumY = 4,
                    });

                    model.Annotations.Add(new TextAnnotation
                    {
                        Text = text,
                        TextPosition = new DataPoint(i * 24 * 60 + input.timeMinutes, textY),
                        TextColor = colorWithOpacity,
                        Font = "Consolas",
                        StrokeThickness = 0,
                    });

                });
            }

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
            inputLabel = new Label();
            searchButton = new Button();
            SuspendLayout();
            // 
            // plot
            // 
            plot.Dock = DockStyle.Left;
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
            // 
            // inputLabel
            // 
            inputLabel.Location = new Point(927, 9);
            inputLabel.Name = "inputLabel";
            inputLabel.Size = new Size(228, 313);
            inputLabel.TabIndex = 1;
            inputLabel.Text = "label1";
            // 
            // searchButton
            // 
            searchButton.Location = new Point(927, 325);
            searchButton.Name = "searchButton";
            searchButton.Size = new Size(228, 23);
            searchButton.TabIndex = 2;
            searchButton.Text = "Search for better dosage";
            searchButton.UseVisualStyleBackColor = true;
            searchButton.Click += searchButton_Click;
            // 
            // SimulationForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1167, 360);
            Controls.Add(searchButton);
            Controls.Add(inputLabel);
            Controls.Add(plot);
            Margin = new Padding(4, 3, 4, 3);
            Name = "SimulationForm";
            Text = "MDI Simulator";
            ResumeLayout(false);
        }

        private PlotView plot;
        private Label inputLabel;
        private Button searchButton;

        private void searchButton_Click(object sender, EventArgs e)
        {
            searchButton.Enabled = false;
            searchButton.UseWaitCursor = true;
            searchButton.Text = "Searching...";

            var search = new GeneticSearch(originalInput);
            search.ga.Population.BestChromosomeChanged += (_, _) =>
            {
                bestInput = GeneticSearch.InputFromChromosome(originalInput, search.ga.BestChromosome);
                Invoke((MethodInvoker)delegate
                {
                    RefreshLayout();
                });
            };

            Task searchTask = Task.Run(() =>
            {
                bestInput = search.FindBetterInput();
                this.Invoke((MethodInvoker)delegate
                {
                    RefreshLayout();
                    searchButton.UseWaitCursor = false;
                    searchButton.Enabled = true;
                    searchButton.Text = "Search for better dosage";
                });

            });
        }
    }
}