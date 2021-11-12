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

        private RectangleF renderViewportCoordinates = new RectangleF();
        private PointF drawShift = new PointF();
        private float scale = 1.0f;
        private int fontSize = 8;

        private bool isMouseDown = false;
        private Point lastMouseLocation = new Point(0, 0);

        private Pen defaultPen = Pens.Black;
        private Brush defaultBrush = Brushes.Black;

        private BufferedGraphicsContext currentContext;
        private BufferedGraphics renderPanelBuffer;

        public Form1()
        {
            InitializeComponent();

            currentContext = BufferedGraphicsManager.Current;
            renderPanelBuffer = currentContext.Allocate(CreateGraphics(), renderPanel.DisplayRectangle);
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            string workingDirectory = Path.GetDirectoryName(Application.StartupPath);
            openPerfFileDialog.InitialDirectory = workingDirectory;

            updateRenderViewportSize();

            setStatusText("Ready. Press \"File -> Open\" to open profile data");
        }

        private void renderToBuffer(Graphics g)
        {
            g.Clear(renderPanel.BackColor);

            Font taskFont = new Font(FontFamily.GenericMonospace, fontSize);

            PointF viewportStartPos = new PointF(renderViewportCoordinates.Left, renderViewportCoordinates.Top);

            float threadHeight = 50.0f * scale;
            float scopeRecordHeight = 10.0f * scale;
            float threadVerticalSpacing = 10.0f * scale;
            float threadLineTotalHeight = threadHeight + threadVerticalSpacing;

            float timeScale = getTimeScale();

            long minVisibleTime = getTimeFromPosition(renderViewportCoordinates.Left - drawShift.X, timeScale);
            long maxVisibleTime = getTimeFromPosition(renderViewportCoordinates.Right - drawShift.X, timeScale);

            g.DrawLine(defaultPen, renderViewportCoordinates.Left, renderViewportCoordinates.Top, renderViewportCoordinates.Right, renderViewportCoordinates.Top);
            for (int frameIndex = 0; frameIndex < visualCaches.frameStartTimes.Count; frameIndex++)
            {
                long frameStart = visualCaches.frameStartTimes[frameIndex];
                float posX = viewportStartPos.X + getPositionFromTime(frameStart, timeScale) + drawShift.X;
                g.DrawLine(defaultPen, posX, renderViewportCoordinates.Top, posX, renderViewportCoordinates.Top + 10);
                g.DrawString(string.Format("Frame{0}", frameIndex), taskFont, defaultBrush, new Point((int)posX, (int)renderViewportCoordinates.Top));
            }

            foreach (FrameData frame in performanceData.frames)
            {
                foreach (TaskData taskData in frame.tasks)
                {
                    int posY = getThreadYPos(taskData.threadId, threadLineTotalHeight, viewportStartPos);
                    renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, threadHeight, taskFont);
                }
            }

            foreach (TaskData taskData in performanceData.nonFrameTasks)
            {
                int posY = getThreadYPos(taskData.threadId, threadLineTotalHeight, viewportStartPos);
                renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, threadHeight, taskFont);
            }

            foreach (ScopeThreadRecords scopeThreadRecords in performanceData.scopeRecords)
            {
                int threadPosY = getThreadYPos(scopeThreadRecords.threadId, threadLineTotalHeight, viewportStartPos) + (int)threadHeight;
                foreach (ScopeRecord record in scopeThreadRecords.records)
                {
                    renderScopeRecord(g, record, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, threadPosY, scopeRecordHeight, taskFont);
                }
            }
        }

        private void renderTaskData(Graphics g, TaskData taskData, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int posY, float threadHeight, Font taskFont)
        {
            if (taskData.timeFinish > minVisibleTime && taskData.timeStart < maxVisibleTime)
            {
                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(taskData.timeStart, timeScale) + drawShift.X), posY);
                Size boxSize = new Size((int)scaleTimeToScreen(taskData.timeFinish - taskData.timeStart, timeScale), (int)threadHeight);
                g.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));

                float offsettedX = Math.Max(boxStartPos.X, (int)renderViewportCoordinates.Left);
                RectangleF textRectangle = new RectangleF(offsettedX, boxStartPos.Y, boxSize.Width, boxSize.Height);
                g.DrawString(performanceData.taskNames[taskData.taskNameIdx], taskFont, defaultBrush, textRectangle);
            }
        }

        private void renderScopeRecord(Graphics g, ScopeRecord scopeRecord, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int threadPosY, float scopeRecordHeight, Font taskFont)
        {
            if (scopeRecord.timeFinish > minVisibleTime && scopeRecord.timeStart < maxVisibleTime)
            {
                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(scopeRecord.timeStart, timeScale) + drawShift.X), (int)(threadPosY + scopeRecordHeight * scopeRecord.stackDepth));
                Size boxSize = new Size((int)scaleTimeToScreen(scopeRecord.timeFinish - scopeRecord.timeStart, timeScale), (int)scopeRecordHeight);
                g.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));

                RectangleF textRectangle = new RectangleF(boxStartPos.X, boxStartPos.Y, boxSize.Width, boxSize.Height);
                g.DrawString(scopeRecord.scopeName, taskFont, defaultBrush, textRectangle);
            }
        }

        private void drawTextForBox()
        {
        }

        private int getThreadYPos(int threadId, float threadLineTotalHeight, PointF viewportStartPos)
        {
            int threadIndex = visualCaches.threads.IndexOf(threadId);
            return (int)(viewportStartPos.Y + threadIndex * threadLineTotalHeight + drawShift.Y);
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
                render();
            }
        }

        private void trackBar1_Scroll(object sender, System.EventArgs e)
        {
            float previousScale = scale;
            scale = (1 + scaleTrackBar.Value) / ((scaleTrackBar.Maximum + 2) * 0.5f);

            PointF scaleCenterPosition = new PointF(renderViewportCoordinates.Width / 2.0f, renderViewportCoordinates.Height / 2.0f);

            drawShift.X = drawShift.X * scale / previousScale - (scaleCenterPosition.X * scale / previousScale - scaleCenterPosition.X);
            drawShift.Y = drawShift.Y * scale / previousScale - (scaleCenterPosition.Y * scale / previousScale - scaleCenterPosition.Y);

            render();
        }

        private void openPerfFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {

            try
            {
                performanceData = PerformanceDataJsonReader.ReadFromJson(openPerfFileDialog.FileName);
                visualCaches.updateFromData(performanceData);
                render();
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
                render();
            }
        }

        private void renderPanel_Resize(object sender, System.EventArgs e)
        {
            renderPanelBuffer = currentContext.Allocate(CreateGraphics(), renderPanel.DisplayRectangle);
            updateRenderViewportSize();
            render();
        }

        private void setStatusText(string newStatusText)
        {
            toolStripStatusLabel.Text = newStatusText;
        }

        private void trackBar2_Scroll(object sender, System.EventArgs e)
        {
            fontSize = fontSizeTrackBar.Value;
            render();
        }

        private void framesTrackBar_Scroll(object sender, System.EventArgs e)
        {
            if (framesTrackBar.Value >= 0 && framesTrackBar.Value < visualCaches.frameStartTimes.Count)
            {
                drawShift.X = -getPositionFromTime(visualCaches.frameStartTimes[framesTrackBar.Value], getTimeScale());
                render();
            }
        }

        private void updateFrameTrack()
        {
            framesTrackBar.Maximum = Math.Max(performanceData.frames.Count - 1, 0);
        }

        private float getPositionFromTime(long time, float timeScale)
        {
            return scaleTimeToScreen(time - visualCaches.minTime, timeScale);
        }

        private long getTimeFromPosition(float positionX, float timeScale)
        {
            return visualCaches.minTime + Convert.ToInt64(positionX / timeScale);
        }

        private float scaleTimeToScreen(long time, float timeScale)
        {
            return time * timeScale;
        }

        private float getTimeScale()
        {
            return renderViewportCoordinates.Width / visualCaches.averageFrameDuration * scale;
        }

        private void render()
        {
            renderToBuffer(renderPanelBuffer.Graphics);
            renderPanelBuffer.Render();
            renderPanelBuffer.Render(renderPanel.CreateGraphics());
        }
    }
}
