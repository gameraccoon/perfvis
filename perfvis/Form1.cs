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
        private class Selection
        {
            public Selection(Type type, int groupIdx, int recordIdx)
            {
                this.type = type;
                this.groupIdx = groupIdx;
                this.recordIdx = recordIdx;
            }

            public enum Type
            {
                ThreadTask,
                NonThreadTask,
                ScopeRecord
            }

            public Type type;
            public int groupIdx;
            public int recordIdx;
        }

        private PerformanceData performanceData = new PerformanceData();
        private PerformanceVisualizationCaches visualCaches = new PerformanceVisualizationCaches();

        private RectangleF renderViewportCoordinates = new RectangleF();
        private PointF drawShift = new PointF();
        private float scale = 1.0f;
        private int fontSize = 8;
        private const int defaultScopeFontSize = 6;

        private const float threadHeight = 50.0f;
        private const float scopeRecordHeight = 10.0f;
        private const float threadVerticalSpacing = 10.0f;
        private const float threadLineTotalHeight = threadHeight + threadVerticalSpacing;

        private const float minimalTimeSize = 100.0f;

        private bool isMouseDown = false;
        private Point lastMouseLocation = new Point(0, 0);

        private Pen defaultPen = Pens.Black;
        private Brush defaultBrush = Brushes.Black;
        private SolidBrush taskBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Snow);
        private SolidBrush hoveredElementBrush = new System.Drawing.SolidBrush(System.Drawing.Color.CornflowerBlue);

        private BufferedGraphicsContext currentContext;
        private BufferedGraphics renderPanelBuffer;

        private Selection hoveredElement;
        private const float maxMoveDeltaToSelect = 2.0f;
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
            Font scopeRecordFont = new Font(FontFamily.GenericMonospace, defaultScopeFontSize * scale);

            PointF viewportStartPos = new PointF(renderViewportCoordinates.Left, renderViewportCoordinates.Top);

            float timeScale = getTimeScale();

            long minVisibleTime = getTimeFromPosition(renderViewportCoordinates.Left - drawShift.X, timeScale);
            long maxVisibleTime = getTimeFromPosition(renderViewportCoordinates.Right - drawShift.X, timeScale);

            TaskData hoveredTaskData = getTaskFromSelectaion(hoveredElement);
            ScopeRecord hoveredScopeRecord = getScopeRecordFromSelection(hoveredElement);

            renderTimeRuler(g, viewportStartPos, taskFont, timeScale);
            renderFrameRuler(g, viewportStartPos, taskFont, timeScale);

            foreach (FrameData frame in performanceData.frames)
            {
                foreach (TaskData taskData in frame.tasks)
                {
                    int posY = getThreadYPos(taskData.threadId);
                    renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, taskFont, taskData == hoveredTaskData);
                }
            }

            foreach (TaskData taskData in performanceData.nonFrameTasks)
            {
                int posY = getThreadYPos(taskData.threadId);
                renderTaskData(g, taskData, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, posY, taskFont, taskData == hoveredTaskData);
            }

            foreach (ScopeThreadRecords scopeThreadRecords in performanceData.scopeRecords)
            {
                if (threadStackFoldings[visualCaches.threads.IndexOf(scopeThreadRecords.threadId)])
                {
                    int threadPosY = getThreadYPos(scopeThreadRecords.threadId) + (int)(threadHeight * scale);
                    foreach (ScopeRecord record in scopeThreadRecords.records)
                    {
                        renderScopeRecord(g, record, minVisibleTime, maxVisibleTime, viewportStartPos, timeScale, threadPosY, scopeRecordFont, record == hoveredScopeRecord);
                    }
                }
            }

            if (hoveredTaskData != null)
            {
                g.DrawString(performanceData.taskNames[hoveredTaskData.taskNameIdx], taskFont, defaultBrush, new Point(30, 30));
            }

            if (hoveredScopeRecord != null)
            {
                g.DrawString(hoveredScopeRecord.scopeName, taskFont, defaultBrush, new Point(30, 30));
            }
        }

        private void renderTaskData(Graphics g, TaskData taskData, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int posY, Font taskFont, bool isHovered)
        {
            if (taskData.timeFinish > minVisibleTime && taskData.timeStart < maxVisibleTime)
            {
                SolidBrush brush = taskBrush;
                if (isHovered)
                {
                    brush = hoveredElementBrush;
                }

                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(taskData.timeStart, timeScale) + drawShift.X), posY);
                Size boxSize = new Size((int)scaleTimeToScreen(taskData.timeFinish - taskData.timeStart, timeScale), (int)(threadHeight * scale));
                g.FillRectangle(brush, new Rectangle(boxStartPos, boxSize));
                g.DrawRectangle(defaultPen, new Rectangle(boxStartPos, boxSize));
                renderTextForBox(g, performanceData.taskNames[taskData.taskNameIdx], boxStartPos, boxSize, taskFont);
            }
        }

        private void renderScopeRecord(Graphics g, ScopeRecord scopeRecord, long minVisibleTime, long maxVisibleTime, PointF viewportStartPos, float timeScale, int threadPosY, Font taskFont, bool isHovered)
        {
            if (scopeRecord.timeFinish > minVisibleTime && scopeRecord.timeStart < maxVisibleTime)
            {
                SolidBrush brush = taskBrush;
                if (isHovered)
                {
                    brush = hoveredElementBrush;
                }

                Point boxStartPos = new Point((int)(viewportStartPos.X + getPositionFromTime(scopeRecord.timeStart, timeScale) + drawShift.X), (int)(threadPosY + scopeRecordHeight * scale * scopeRecord.stackDepth));
                Size boxSize = new Size((int)scaleTimeToScreen(scopeRecord.timeFinish - scopeRecord.timeStart, timeScale), (int)(scopeRecordHeight * scale));
                g.FillRectangle(brush, new Rectangle(boxStartPos, boxSize));
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

        private void renderTimeRuler(Graphics g, PointF viewportStartPos, Font taskFont, float timeScale)
        {
            long globalTimeStart = visualCaches.minTime;
            long timeMin = getTimeFromPosition(renderViewportCoordinates.Left - drawShift.X, timeScale) - globalTimeStart;
            long timeMax = getTimeFromPosition(renderViewportCoordinates.Right - drawShift.X, timeScale) - globalTimeStart;

            double oneUnitScale = performanceData.config.timeUnitScale;

            double itemsCanFit = renderViewportCoordinates.Width / minimalTimeSize;
            double itemSize = floorToPowerOf(Math.Max(1, (timeMax - timeMin) / itemsCanFit) / oneUnitScale, 10.0) * oneUnitScale;

            double timeStart = (long)Math.Ceiling((double)timeMin / itemSize) * itemSize;
            double timeStep = itemSize;

            g.DrawLine(defaultPen, renderViewportCoordinates.Left, renderViewportCoordinates.Top, renderViewportCoordinates.Right, renderViewportCoordinates.Top);
            for (double timePoint = timeStart; timePoint < timeMax; timePoint += timeStep)
            {
                float posX = viewportStartPos.X + getPositionFromTime(globalTimeStart + (long)timePoint, timeScale) + drawShift.X;
                g.DrawLine(defaultPen, posX, renderViewportCoordinates.Top, posX, renderViewportCoordinates.Top + 10);
                g.DrawString(string.Format("{0} " + performanceData.config.timeUnit, timePoint / oneUnitScale), taskFont, defaultBrush, new Point((int)posX + 2, (int)renderViewportCoordinates.Top));
            }
        }

        private void renderFrameRuler(Graphics g, PointF viewportStartPos, Font taskFont, float timeScale)
        {
            for (int frameIndex = 0; frameIndex < visualCaches.frameStartTimes.Count; frameIndex++)
            {
                long frameStart = visualCaches.frameStartTimes[frameIndex];
                float posX = viewportStartPos.X + getPositionFromTime(frameStart, timeScale) + drawShift.X;
                g.DrawLine(defaultPen, posX, renderViewportCoordinates.Top + 10, posX, renderViewportCoordinates.Top + 20);
                g.DrawString(string.Format("Frame{0}", frameIndex), taskFont, defaultBrush, new Point((int)posX + 2, (int)renderViewportCoordinates.Top + 12));
            }
        }

        private int getThreadYPos(int threadId)
        {
            int threadIndex = visualCaches.threads.IndexOf(threadId);
            return getThreadYPosByIndex(threadIndex);
        }

        private int getThreadYPosByIndex(int threadIndex)
        {
            if (threadIndex >= 0 && threadIndex < cachedThreadHeights.Count)
            {
                return (int)(renderViewportCoordinates.Top + cachedThreadHeights[threadIndex] * scale + drawShift.Y);
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
            float threadMin = getThreadYPosByIndex(threadIdx);
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
                int threadIndex = getThreadIdxFromPosition(e.Y);
                if (threadIndex >= 0 && threadIndex < threadStackFoldings.Count && isPosInsideThreadBlock(e.Y, threadIndex))
                {
                    threadStackFoldings[threadIndex] = !threadStackFoldings[threadIndex];
                    updateThreadHeights();
                    render();
                }
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

            if (threadIdx >= 0 && threadIdx < visualCaches.threads.Count)
            {
                int threadId = visualCaches.threads[threadIdx];

                if (isPosInsideThreadBlock(mouseY, threadIdx))
                {
                    for (int frameIdx = 0; frameIdx < performanceData.frames.Count; frameIdx++)
                    {
                        FrameData frame = performanceData.frames[frameIdx];
                        for (int taskIdx = 0; taskIdx < frame.tasks.Count; taskIdx++)
                        {
                            TaskData taskData = frame.tasks[taskIdx];
                            if (taskData.threadId == threadId && taskData.timeStart < time && time < taskData.timeFinish)
                            {
                                updateHoveredElementIfChanged(Selection.Type.ThreadTask, frameIdx, taskIdx);
                                return;
                            }
                        }
                    }

                    for (int taskIdx = 0; taskIdx < performanceData.nonFrameTasks.Count; taskIdx++)
                    {
                        TaskData taskData = performanceData.nonFrameTasks[taskIdx];
                        if (taskData.threadId == threadId && taskData.timeStart < time && time < taskData.timeFinish)
                        {
                            updateHoveredElementIfChanged(Selection.Type.NonThreadTask, 0, taskIdx);
                            return;
                        }
                    }
                }

                int scopeDepth = getScopeDepthFromYPos(mouseY, threadIdx);
                if (scopeDepth > 0 && scopeDepth <= visualCaches.scopedRecordsDepthByThread[threadIdx])
                {
                    for (int threadRecordIdx = 0; threadRecordIdx < performanceData.scopeRecords.Count; threadRecordIdx++)
                    {
                        ScopeThreadRecords scopeThreadRecords = performanceData.scopeRecords[threadRecordIdx];
                        if (scopeThreadRecords.threadId == threadId)
                        {
                            for (int recordIdx = 0; recordIdx < scopeThreadRecords.records.Count; recordIdx++)
                            {
                                ScopeRecord record = scopeThreadRecords.records[recordIdx];
                                if (record.stackDepth == scopeDepth && record.timeStart < time && time < record.timeFinish)
                                {
                                    updateHoveredElementIfChanged(Selection.Type.ScopeRecord, threadRecordIdx, recordIdx);
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            resetHoveredElement();
        }

        private void updateHoveredElementIfChanged(Selection.Type type, int groupIdx, int recordIdx)
        {
            if (hoveredElement == null || hoveredElement.type != type || hoveredElement.groupIdx != groupIdx || hoveredElement.recordIdx != recordIdx)
            {
                hoveredElement = new Selection(type, groupIdx, recordIdx);
                render();
            }
        }

        private void resetHoveredElement()
        {
            if (hoveredElement != null)
            {
                hoveredElement = null;
                render();
            }
        }

        private TaskData getTaskFromSelectaion(Selection selection)
        {
            if (selection == null)
            {
                return null;
            }

            if (selection.type == Selection.Type.ThreadTask)
            {
                return performanceData.frames[selection.groupIdx].tasks[selection.recordIdx];
            }
            else if (selection.type == Selection.Type.NonThreadTask)
            {
                return performanceData.nonFrameTasks[selection.recordIdx];
            }
            return null;
        }

        private ScopeRecord getScopeRecordFromSelection(Selection selection)
        {
            if (selection == null)
            {
                return null;
            }

            if (selection.type == Selection.Type.ScopeRecord)
            {
                return performanceData.scopeRecords[selection.groupIdx].records[selection.recordIdx];
            }
            return null;
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

        private int getScopeDepthFromYPos(float positionY, int threadIdx)
        {
            int threadYPos = getThreadYPosByIndex(threadIdx);
            return (int)Math.Floor((positionY - threadYPos - threadLineTotalHeight * scale) / (scopeRecordHeight * scale)) + 1;
        }

        private double floorToPowerOf(double value, double power)
        {
            return Math.Pow(power, Math.Ceiling(Math.Log(value, power)));
        }
    }
}
