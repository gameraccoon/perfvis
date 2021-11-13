using perfvis.Caches;
using perfvis.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace perfvis
{
    public partial class Form1 : Form
    {
        private class TaskSelection
        {
            public TaskSelection(Type type, int frameIdx, int taskIdx)
            {
                this.type = type;
                this.frameIdx = frameIdx;
                this.taskIdx = taskIdx;
            }

            public enum Type
            {
                ThreadTask,
                NonThreadTask
            }

            public Type type;
            public int frameIdx;
            public int taskIdx;
        }

        private PerformanceData performanceData = new PerformanceData();
        private PerformanceVisualizationCaches visualCaches = new PerformanceVisualizationCaches();

        private RectangleF renderViewportCoordinates = new RectangleF();
        private PointF drawShift = new PointF();
        private float scale = 1.0f;
        private int fontSize = 8;

        private const float threadHeight = 50.0f;
        private const float scopeRecordHeight = 10.0f;
        private const float threadVerticalSpacing = 10.0f;
        private const float threadLineTotalHeight = threadHeight + threadVerticalSpacing;

        private bool isMouseDown = false;
        private Point lastMouseLocation = new Point(0, 0);

        private Pen defaultPen = Pens.Black;
        private Brush defaultBrush = Brushes.Black;
        private SolidBrush taskBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Snow);
        private SolidBrush hoveredTaskBrush = new System.Drawing.SolidBrush(System.Drawing.Color.CornflowerBlue);

        private BufferedGraphicsContext currentContext;
        private BufferedGraphics renderPanelBuffer;

        private TaskSelection hoveredTask;
        private const float maxMoveDeltaToSelect = 10.0f;
        private const float maxMoveDeltaToSelectSqr = maxMoveDeltaToSelect * maxMoveDeltaToSelect;
        private bool moveExceededSelectRange = false;
        private Point mouseDownPos = new Point();

        private List<bool> threadStackFoldings = new List<bool>();
        private List<int> cachedThreadHeights = new List<int>();

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

            renderPanel.MouseWheel += new MouseEventHandler(renderPanel_MouseWheel);

            currentContext = BufferedGraphicsManager.Current;
            renderPanelBuffer = currentContext.Allocate(CreateGraphics(), renderPanel.DisplayRectangle);
        }

        private void renderToBuffer(Graphics g)
        {
            g.Clear(renderPanel.BackColor);

            Font taskFont = new Font(FontFamily.GenericMonospace, fontSize);

            PointF viewportStartPos = new PointF(renderViewportCoordinates.Left, renderViewportCoordinates.Top);

            float timeScale = getTimeScale();

            long minVisibleTime = getTimeFromPosition(renderViewportCoordinates.Left - drawShift.X, timeScale);
            long maxVisibleTime = getTimeFromPosition(renderViewportCoordinates.Right - drawShift.X, timeScale);

            TaskData hoveredTaskData = getTaskFromSelectaion(hoveredTask);

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
                    int posY = getThreadYPos(taskData.threadId, viewportStartPos);
                    renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, taskFont, taskData == hoveredTaskData);
                }
            }

            foreach (TaskData taskData in performanceData.nonFrameTasks)
            {
                int posY = getThreadYPos(taskData.threadId, viewportStartPos);
                renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, taskFont, taskData == hoveredTaskData);
            }

            foreach (ScopeThreadRecords scopeThreadRecords in performanceData.scopeRecords)
            {
                if (threadStackFoldings[visualCaches.threads.IndexOf(scopeThreadRecords.threadId)])
                {
                    int threadPosY = getThreadYPos(scopeThreadRecords.threadId, viewportStartPos) + (int)(threadHeight * scale);
                    foreach (ScopeRecord record in scopeThreadRecords.records)
                    {
                        renderScopeRecord(g, record, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, threadPosY, taskFont);
                    }
                }
            }
        }

        private void renderTaskData(Graphics g, TaskData taskData, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int posY, Font taskFont, bool isHovered)
        {
            if (taskData.timeFinish > minVisibleTime && taskData.timeStart < maxVisibleTime)
            {
                SolidBrush brush = taskBrush;
                if (isHovered)
                {
                    brush = hoveredTaskBrush;
                }

                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(taskData.timeStart, timeScale) + drawShift.X), posY);
                Size boxSize = new Size((int)scaleTimeToScreen(taskData.timeFinish - taskData.timeStart, timeScale), (int)(threadHeight * scale));
                g.FillRectangle(brush, new Rectangle(boxStartPos, boxSize));
                g.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));
                renderTextForBox(g, performanceData.taskNames[taskData.taskNameIdx], boxStartPos, boxSize, taskFont);
            }
        }

        private void renderScopeRecord(Graphics g, ScopeRecord scopeRecord, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int threadPosY, Font taskFont)
        {
            if (scopeRecord.timeFinish > minVisibleTime && scopeRecord.timeStart < maxVisibleTime)
            {
                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(scopeRecord.timeStart, timeScale) + drawShift.X), (int)(threadPosY + scopeRecordHeight * scale * scopeRecord.stackDepth));
                Size boxSize = new Size((int)scaleTimeToScreen(scopeRecord.timeFinish - scopeRecord.timeStart, timeScale), (int)(scopeRecordHeight * scale));
                g.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));
                renderTextForBox(g, scopeRecord.scopeName, boxStartPos, boxSize, taskFont);
            }
        }

        private void renderTextForBox(Graphics g, string text, Point boxStartPos, Size boxSize, Font taskFont)
        {
            float positionX = Math.Max(boxStartPos.X, (int)renderViewportCoordinates.Left);
            float width = boxSize.Width - positionX + boxStartPos.X;
            float height = boxSize.Height;
            if (width > 1.0f && height > 1.0f)
            {
                RectangleF textRectangle = new RectangleF(positionX, boxStartPos.Y, width, height);
                g.DrawString(text, taskFont, defaultBrush, textRectangle);
            }
        }

        private int getThreadYPos(int threadId, PointF viewportStartPos)
        {
            int threadIndex = visualCaches.threads.IndexOf(threadId);
            return getThreadYPosByIndex(threadIndex, viewportStartPos);
        }

        private int getThreadYPosByIndex(int threadIndex, PointF viewportStartPos)
        {
            if (threadIndex >= 0 && threadIndex < cachedThreadHeights.Count)
            {
                return (int)(viewportStartPos.Y + cachedThreadHeights[threadIndex] * scale + drawShift.Y);
            }
            return 0;
        }

        private int getThreadIdxFromPosition(float positionY)
        {
            float relativePosY = (positionY - drawShift.Y) / scale;
            for (int i = cachedThreadHeights.Count - 1; i >= 0; i--)
            {
                if (relativePosY >= cachedThreadHeights[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private bool isPosInsideThreadBlock(float mouseY, int threadIdx)
        {
            PointF viewportStartPos = new PointF(renderViewportCoordinates.Left, renderViewportCoordinates.Top);
            float threadMin = getThreadYPosByIndex(threadIdx, viewportStartPos);
            float threadMax = threadMin + threadHeight * scale;
            return threadMin < mouseY && mouseY <= threadMax;
        }

        private void updateRenderViewportSize()
        {
            renderViewportCoordinates.X = 0;
            renderViewportCoordinates.Y = 0;
            renderViewportCoordinates.Width = renderPanel.Width;
            renderViewportCoordinates.Height = renderPanel.Height;
        }

        private void trackBar1_Scroll(object sender, System.EventArgs e)
        {
            PointF scaleCenterPosition = new PointF(renderViewportCoordinates.Width / 2.0f, renderViewportCoordinates.Height / 2.0f);
            scaleInto(scaleCenterPosition, getScaleFromScroll());

            render();
        }

        private float getScaleFromScroll()
        {
            return (1 + scaleTrackBar.Value) / ((scaleTrackBar.Maximum + 2) * 0.5f); ;
        }

        private void scaleInto(PointF scaleCenterPosition, float newScale)
        {
            float previousScale = scale;
            scale = (1 + scaleTrackBar.Value) / ((scaleTrackBar.Maximum + 2) * 0.5f);

            drawShift.X = drawShift.X * scale / previousScale - (scaleCenterPosition.X * scale / previousScale - scaleCenterPosition.X);
            drawShift.Y = drawShift.Y * scale / previousScale - (scaleCenterPosition.Y * scale / previousScale - scaleCenterPosition.Y);
        }

        private void openPerfFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                performanceData = PerformanceDataJsonReader.ReadFromJson(openPerfFileDialog.FileName);
                visualCaches.updateFromData(performanceData);
                threadStackFoldings = new List<bool>(new bool[visualCaches.threads.Count]);
                cachedThreadHeights = new List<int>(new int[visualCaches.threads.Count]);
                updateThreadHeights();
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
            mouseDownPos.X = e.X;
            mouseDownPos.Y = e.Y;
            moveExceededSelectRange = false;
        }

        private void renderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            isMouseDown = false;

            if (!moveExceededSelectRange)
            {
                TaskData hoveredTaskData = getTaskFromSelectaion(hoveredTask);
                if (hoveredTaskData != null)
                {
                    int threadIndex = visualCaches.threads.IndexOf(hoveredTaskData.threadId);
                    threadStackFoldings[threadIndex] = !threadStackFoldings[threadIndex];
                    updateThreadHeights();
                }
                render();
            }
        }

        private void renderPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                drawShift.X += e.X - lastMouseLocation.X;
                drawShift.Y += e.Y - lastMouseLocation.Y;
                lastMouseLocation.X = e.X;
                lastMouseLocation.Y = e.Y;

                if (!moveExceededSelectRange)
                {
                    float xDist = lastMouseLocation.X - mouseDownPos.X;
                    float yDist = lastMouseLocation.Y - mouseDownPos.Y;
                    if (xDist * xDist + yDist * yDist > maxMoveDeltaToSelectSqr)
                    {
                        moveExceededSelectRange = true;
                    }
                }

                render();
            }
            else
            {
                updateHoveredElement(e.X, e.Y);
            }
        }

        private void renderPanel_Resize(object sender, System.EventArgs e)
        {
            if (currentContext != null)
            {
                renderPanelBuffer = currentContext.Allocate(CreateGraphics(), renderPanel.DisplayRectangle);
                updateRenderViewportSize();
                render();
            }
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

        private void renderPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                scaleTrackBar.Value = Math.Min(scaleTrackBar.Value + 1, scaleTrackBar.Maximum);
            }
            else
            {
                scaleTrackBar.Value = Math.Max(scaleTrackBar.Value - 1, scaleTrackBar.Minimum);
            }

            PointF scaleCenterPosition = new PointF(e.X - renderViewportCoordinates.X, e.Y - renderViewportCoordinates.Y);
            scaleInto(scaleCenterPosition, getScaleFromScroll());

            render();
        }

        private void updateHoveredElement(float mouseX, float mouseY)
        {
            float timeScale = getTimeScale();
            long time = getTimeFromPosition(mouseX - drawShift.X, timeScale);
            int threadIdx = getThreadIdxFromPosition(mouseY);

            if (threadIdx >= 0 && threadIdx < visualCaches.threads.Count && isPosInsideThreadBlock(mouseY, threadIdx))
            {
                int threadId = visualCaches.threads[threadIdx];

                for (int frameIdx = 0; frameIdx < performanceData.frames.Count; frameIdx++)
                {
                    FrameData frame = performanceData.frames[frameIdx];
                    for (int taskIdx = 0; taskIdx < frame.tasks.Count; taskIdx++)
                    {
                        TaskData taskData = frame.tasks[taskIdx];
                        if (taskData.threadId == threadId && taskData.timeStart < time && time < taskData.timeFinish)
                        {
                            updateHoveredElementIfChanged(TaskSelection.Type.ThreadTask, frameIdx, taskIdx);
                            return;
                        }
                    }
                }

                for (int taskIdx = 0; taskIdx < performanceData.nonFrameTasks.Count; taskIdx++)
                {
                    TaskData taskData = performanceData.nonFrameTasks[taskIdx];
                    if (taskData.threadId == threadId && taskData.timeStart < time && time < taskData.timeFinish)
                    {
                        updateHoveredElementIfChanged(TaskSelection.Type.NonThreadTask, 0, taskIdx);
                        return;
                    }
                }
            }

            resetHoveredElement();
        }

        private void updateHoveredElementIfChanged(TaskSelection.Type type, int frameIdx, int taskIdx)
        {
            if (hoveredTask == null || hoveredTask.type != type || hoveredTask.frameIdx != frameIdx || hoveredTask.taskIdx != taskIdx)
            {
                hoveredTask = new TaskSelection(type, frameIdx, taskIdx);
                render();
            }
        }

        private void resetHoveredElement()
        {
            if (hoveredTask != null)
            {
                hoveredTask = null;
                render();
            }
        }

        private TaskData getTaskFromSelectaion(TaskSelection selection)
        {
            if (selection == null)
            {
                return null;
            }

            if (selection.type == TaskSelection.Type.ThreadTask)
            {
                return performanceData.frames[selection.frameIdx].tasks[selection.taskIdx];
            }
            else
            {
                return performanceData.nonFrameTasks[selection.taskIdx];
            }
        }

        private void updateThreadHeights()
        {
            int height = 0;
            for (int i = 0; i < visualCaches.threads.Count; i++)
            {
                cachedThreadHeights[i] = height;

                height += (int)threadLineTotalHeight;

                if (threadStackFoldings[i])
                {
                    height += (int)(visualCaches.scopedRecordsDepthByThread[i] * scopeRecordHeight + scopeRecordHeight);
                }
            }
        }
    }
}
