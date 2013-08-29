namespace VMDSDispatcher
{
    partial class frmMain
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
            this.components = new System.ComponentModel.Container();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.tsslGatewayState = new System.Windows.Forms.ToolStripStatusLabel();
            this.lbMessage = new System.Windows.Forms.ListBox();
            this.tmMaintimer = new System.Windows.Forms.Timer(this.components);
            this.btnRUN = new System.Windows.Forms.Button();
            this.tmChkProgram = new System.Windows.Forms.Timer(this.components);
            this.lvDebugInfo = new System.Windows.Forms.ListView();
            this.chTimeStamp = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chSeverity = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chFunction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chDebugInfo = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnSTOP = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsslGatewayState});
            this.statusStrip1.Location = new System.Drawing.Point(0, 430);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(634, 22);
            this.statusStrip1.TabIndex = 0;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // tsslGatewayState
            // 
            this.tsslGatewayState.Name = "tsslGatewayState";
            this.tsslGatewayState.Size = new System.Drawing.Size(79, 17);
            this.tsslGatewayState.Text = "Disconnected";
            // 
            // lbMessage
            // 
            this.lbMessage.FormattingEnabled = true;
            this.lbMessage.Location = new System.Drawing.Point(-1, 194);
            this.lbMessage.Name = "lbMessage";
            this.lbMessage.Size = new System.Drawing.Size(635, 238);
            this.lbMessage.TabIndex = 1;
            // 
            // tmMaintimer
            // 
            this.tmMaintimer.Interval = 60000;
            this.tmMaintimer.Tick += new System.EventHandler(this.tmMaintimer_Tick);
            // 
            // btnRUN
            // 
            this.btnRUN.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnRUN.Location = new System.Drawing.Point(0, -1);
            this.btnRUN.Name = "btnRUN";
            this.btnRUN.Size = new System.Drawing.Size(120, 23);
            this.btnRUN.TabIndex = 9;
            this.btnRUN.Text = "Start Dispatch";
            this.btnRUN.UseVisualStyleBackColor = true;
            this.btnRUN.Click += new System.EventHandler(this.btnRUN_Click);
            // 
            // tmChkProgram
            // 
            this.tmChkProgram.Interval = 300000;
            this.tmChkProgram.Tick += new System.EventHandler(this.tmChkProgram_Tick);
            // 
            // lvDebugInfo
            // 
            this.lvDebugInfo.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chTimeStamp,
            this.chSeverity,
            this.chFunction,
            this.chDebugInfo});
            this.lvDebugInfo.FullRowSelect = true;
            this.lvDebugInfo.GridLines = true;
            this.lvDebugInfo.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvDebugInfo.Location = new System.Drawing.Point(0, 28);
            this.lvDebugInfo.MultiSelect = false;
            this.lvDebugInfo.Name = "lvDebugInfo";
            this.lvDebugInfo.Size = new System.Drawing.Size(634, 160);
            this.lvDebugInfo.TabIndex = 10;
            this.lvDebugInfo.UseCompatibleStateImageBehavior = false;
            this.lvDebugInfo.View = System.Windows.Forms.View.Details;
            // 
            // chTimeStamp
            // 
            this.chTimeStamp.Text = "TimeStamp";
            this.chTimeStamp.Width = 160;
            // 
            // chSeverity
            // 
            this.chSeverity.Text = "Severity";
            // 
            // chFunction
            // 
            this.chFunction.Text = "Function";
            this.chFunction.Width = 100;
            // 
            // chDebugInfo
            // 
            this.chDebugInfo.Text = "DebugInfo.";
            this.chDebugInfo.Width = 321;
            // 
            // btnSTOP
            // 
            this.btnSTOP.Enabled = false;
            this.btnSTOP.Location = new System.Drawing.Point(134, -1);
            this.btnSTOP.Name = "btnSTOP";
            this.btnSTOP.Size = new System.Drawing.Size(120, 23);
            this.btnSTOP.TabIndex = 11;
            this.btnSTOP.Text = "Stop Dispatch";
            this.btnSTOP.UseVisualStyleBackColor = true;
            this.btnSTOP.Click += new System.EventHandler(this.btnSTOP_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 452);
            this.Controls.Add(this.btnSTOP);
            this.Controls.Add(this.lvDebugInfo);
            this.Controls.Add(this.btnRUN);
            this.Controls.Add(this.lbMessage);
            this.Controls.Add(this.statusStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "frmMain";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.Shown += new System.EventHandler(this.frmMain_Shown);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel tsslGatewayState;
        private System.Windows.Forms.ListBox lbMessage;
        private System.Windows.Forms.Timer tmMaintimer;
        private System.Windows.Forms.Button btnRUN;
        private System.Windows.Forms.Timer tmChkProgram;
        private System.Windows.Forms.ListView lvDebugInfo;
        private System.Windows.Forms.ColumnHeader chTimeStamp;
        private System.Windows.Forms.ColumnHeader chSeverity;
        private System.Windows.Forms.ColumnHeader chFunction;
        private System.Windows.Forms.ColumnHeader chDebugInfo;
        private System.Windows.Forms.Button btnSTOP;
    }
}

