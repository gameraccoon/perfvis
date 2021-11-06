using perfvis.Caches;
using perfvis.Model;
using System.Drawing;
using System.Windows.Forms;

namespace perfvis
{
    public partial class Form1 : Form
    {

        private PerformanceData performanceData = new PerformanceData();
        private PerformanceVisualizationCaches visualCaches = new PerformanceVisualizationCaches();
        
        private Point drawShift;
        private float scale = 1.0f;

        private bool isMouseDown = false;
        private Point lastMouseLocation = new Point(0, 0);

        public Form1()
        {
            InitializeComponent();

            populateTestData();

            ResizeRedraw = true;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            RectangleF visibleClipBounds = e.Graphics.VisibleClipBounds;
            Pen defaultPen = Pens.Black;
            Brush defaultBrush = Brushes.Black;
            Font taskFont = new Font(FontFamily.GenericMonospace, (int)(10 * scale));

            Point viewportStartPos = new Point((int)visibleClipBounds.Left, (int)visibleClipBounds.Top);
            Point viewportEndPos = new Point((int)visibleClipBounds.Right, (int)visibleClipBounds.Bottom);

            float threadHeight = 50.0f * scale;
            float threadVerticalSpacing = 10.0f * scale;
            float threadLineTotalHeight = threadHeight + threadVerticalSpacing;

            float timeScale = visibleClipBounds.Width / visualCaches.totalDuration * scale;

            foreach (FrameData frame in performanceData.frames)
            {
                foreach (TaskData taskData in frame.tasks)
                {
                    int index = visualCaches.threads.IndexOf(taskData.threadId);
                    Point boxStartPos = new Point((int)(viewportStartPos.X + (taskData.timeStart - visualCaches.minTime) * timeScale + drawShift.X), (int)(viewportStartPos.Y + index * threadLineTotalHeight + drawShift.Y));
                    Size boxSize = new Size((int)((taskData.timeFinish - taskData.timeStart) * timeScale), (int)threadHeight);
                    e.Graphics.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));
                    e.Graphics.DrawString(performanceData.taskNames[taskData.taskNameIdx], taskFont, defaultBrush, boxStartPos);
                }
            }
        }

        private void populateTestData()
        {
            performanceData = new PerformanceData();
            performanceData.taskNames.Add("Task0");
            performanceData.taskNames.Add("Task1");
            performanceData.taskNames.Add("Task2");
            performanceData.taskNames.Add("Task3");
            performanceData.taskNames.Add("Task4");
            performanceData.taskNames.Add("Task5");

            {
                FrameData frameData = new FrameData();
                frameData.tasks.Add(new TaskData(0, 1000, 3000, 0));
                frameData.tasks.Add(new TaskData(0, 3050, 5000, 1));
                frameData.tasks.Add(new TaskData(0, 5010, 7000, 2));
                frameData.tasks.Add(new TaskData(0, 7000, 8000, 3));
                performanceData.frames.Add(frameData);
            }

            {
                FrameData frameData = new FrameData();
                frameData.tasks.Add(new TaskData(0, 9000, 10000, 0));
                frameData.tasks.Add(new TaskData(0, 10050, 12000, 1));
                frameData.tasks.Add(new TaskData(1, 10070, 13000, 4));
                frameData.tasks.Add(new TaskData(0, 12010, 13000, 2));
                frameData.tasks.Add(new TaskData(0, 13000, 14000, 3));
                performanceData.frames.Add(frameData);
            }

            visualCaches.updateFromData(performanceData);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            isMouseDown = true;
            lastMouseLocation.X = e.X;
            lastMouseLocation.Y = e.Y;
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            isMouseDown = false;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                drawShift.X += e.X - lastMouseLocation.X;
                drawShift.Y += e.Y - lastMouseLocation.Y;
                lastMouseLocation.X = e.X;
                lastMouseLocation.Y = e.Y;
                Invalidate();
            }
        }

        private void Form1_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollOrientation == ScrollOrientation.VerticalScroll)
            {
                scale *= (e.NewValue - e.OldValue);
                Invalidate();
            }
        }

        private void updateDataBtn_Click(object sender, System.EventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, System.EventArgs e)
        {
            scale = (1 + trackBar1.Value) / ((trackBar1.Maximum + 2) * 0.5f);
            Invalidate();
        }
    }
}
