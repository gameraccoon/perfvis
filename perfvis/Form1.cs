using perfvis.Caches;
using perfvis.Model;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace perfvis
{
    public partial class Form1 : Form
    {

        private PerformanceData performanceData = new PerformanceData();
        private PerformanceVisualizationCaches visualCaches = new PerformanceVisualizationCaches();

        private RectangleF renderViewportCoordinates;
        private PointF drawShift;
        private float scale = 1.0f;
        private int fontSize = 8;

        private bool isMouseDown = false;
        private Point lastMouseLocation = new Point(0, 0);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            string workingDirectory = Path.GetDirectoryName(Application.StartupPath);
            openPerfFileDialog.InitialDirectory = workingDirectory;

            updateRenderViewportSize();

            setStatusText("Ready. Press \"File -> Open\" to open profile data");
        }

        private void renderPanel_Paint(object sender, PaintEventArgs e)
        {
            Pen defaultPen = Pens.Black;
            Brush defaultBrush = Brushes.Black;
            Font taskFont = new Font(FontFamily.GenericMonospace, fontSize);

            PointF viewportStartPos = new PointF(renderViewportCoordinates.Left, renderViewportCoordinates.Top);

            float threadHeight = 50.0f * scale;
            float threadVerticalSpacing = 10.0f * scale;
            float threadLineTotalHeight = threadHeight + threadVerticalSpacing;

            foreach (FrameData frame in performanceData.frames)
            {
                foreach (TaskData taskData in frame.tasks)
                {
                    int index = visualCaches.threads.IndexOf(taskData.threadId);
                    Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(taskData.timeStart) + drawShift.X), (int)(viewportStartPos.Y + index * threadLineTotalHeight + drawShift.Y));
                    Size boxSize = new Size((int)scaleTimeToScreen(taskData.timeFinish - taskData.timeStart), (int)threadHeight);
                    e.Graphics.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));
                    e.Graphics.DrawString(performanceData.taskNames[taskData.taskNameIdx], taskFont, defaultBrush, boxStartPos);
                }
            }
        }

        private void updateRenderViewportSize()
        {
            renderViewportCoordinates.X = 0;
            renderViewportCoordinates.Y = 0;
            renderViewportCoordinates.Width = renderPanel.Width;
            renderViewportCoordinates.Height = renderPanel.Height;
        }

        private void Form1_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollOrientation == ScrollOrientation.VerticalScroll)
            {
                scale *= (e.NewValue - e.OldValue);
                renderPanel.Invalidate();
            }
        }

        private void trackBar1_Scroll(object sender, System.EventArgs e)
        {
            float previousScale = scale;
            scale = (1 + scaleTrackBar.Value) / ((scaleTrackBar.Maximum + 2) * 0.5f);

            PointF scaleCenterPosition = new PointF(renderViewportCoordinates.Width / 2.0f, renderViewportCoordinates.Height / 2.0f);

            drawShift.X = drawShift.X * scale / previousScale - (scaleCenterPosition.X * scale / previousScale - scaleCenterPosition.X);
            drawShift.Y = drawShift.Y * scale / previousScale - (scaleCenterPosition.Y * scale / previousScale - scaleCenterPosition.Y);

            renderPanel.Invalidate();
        }

        private void openPerfFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {

            try
            {
                performanceData = PerformanceDataJsonReader.ReadFromJson(openPerfFileDialog.FileName);
                visualCaches.updateFromData(performanceData);
                renderPanel.Invalidate();
            }
            catch (System.Exception exception)
            {
                MessageBox.Show(exception.Message, "Json read error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            setStatusText("Ready");
            updateFrameTrack();
        }

        private void openToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            openPerfFileDialog.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            Application.Exit();
        }

        private void renderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            isMouseDown = true;
            lastMouseLocation.X = e.X;
            lastMouseLocation.Y = e.Y;
        }

        private void renderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            isMouseDown = false;
        }

        private void renderPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                drawShift.X += e.X - lastMouseLocation.X;
                drawShift.Y += e.Y - lastMouseLocation.Y;
                lastMouseLocation.X = e.X;
                lastMouseLocation.Y = e.Y;
                renderPanel.Invalidate();
            }
        }

        private void renderPanel_Resize(object sender, System.EventArgs e)
        {
            updateRenderViewportSize();
            renderPanel.Invalidate();
        }

        private void setStatusText(string newStatusText)
        {
            toolStripStatusLabel.Text = newStatusText;
        }

        private void trackBar2_Scroll(object sender, System.EventArgs e)
        {
            fontSize = fontSizeTrackBar.Value;
            renderPanel.Invalidate();
        }

        private void framesTrackBar_Scroll(object sender, System.EventArgs e)
        {
            if (framesTrackBar.Value >= 0 && framesTrackBar.Value < visualCaches.frameStartTimes.Count)
            {
                drawShift.X = -getPositionFromTime(visualCaches.frameStartTimes[framesTrackBar.Value]);
                renderPanel.Invalidate();
            }
        }

        private void updateFrameTrack()
        {
            framesTrackBar.Maximum = Math.Max(performanceData.frames.Count - 1, 0);
        }

        private float getPositionFromTime(long time)
        {
            return scaleTimeToScreen(time - visualCaches.minTime);
        }

        private float scaleTimeToScreen(long time)
        {
            float timeScale = renderViewportCoordinates.Width / visualCaches.averageFrameDuration * scale;
            return time * timeScale;
        }
    }
}
