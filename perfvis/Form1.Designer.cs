
namespace perfvis
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ScaleBar = new System.Windows.Forms.StatusStrip();
            this.trackBar1 = new System.Windows.Forms.TrackBar();
            this.updateDataBtn = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).BeginInit();
            this.SuspendLayout();
            // 
            // ScaleBar
            // 
            this.ScaleBar.Location = new System.Drawing.Point(0, 428);
            this.ScaleBar.Name = "ScaleBar";
            this.ScaleBar.Size = new System.Drawing.Size(800, 22);
            this.ScaleBar.TabIndex = 0;
            this.ScaleBar.Text = "statusStrip1";
            // 
            // trackBar1
            // 
            this.trackBar1.Location = new System.Drawing.Point(536, 380);
            this.trackBar1.Name = "trackBar1";
            this.trackBar1.Size = new System.Drawing.Size(264, 45);
            this.trackBar1.TabIndex = 1;
            this.trackBar1.Value = 5;
            this.trackBar1.Scroll += new System.EventHandler(this.trackBar1_Scroll);
            // 
            // updateDataBtn
            // 
            this.updateDataBtn.Location = new System.Drawing.Point(713, 351);
            this.updateDataBtn.Name = "updateDataBtn";
            this.updateDataBtn.Size = new System.Drawing.Size(75, 23);
            this.updateDataBtn.TabIndex = 2;
            this.updateDataBtn.Text = "Update";
            this.updateDataBtn.UseVisualStyleBackColor = true;
            this.updateDataBtn.Click += new System.EventHandler(this.updateDataBtn_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.updateDataBtn);
            this.Controls.Add(this.trackBar1);
            this.Controls.Add(this.ScaleBar);
            this.Name = "Form1";
            this.Text = "perfvis";
            this.Scroll += new System.Windows.Forms.ScrollEventHandler(this.Form1_Scroll);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip ScaleBar;
        private System.Windows.Forms.TrackBar trackBar1;
        private System.Windows.Forms.Button updateDataBtn;
    }
}

