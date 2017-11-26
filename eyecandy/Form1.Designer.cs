namespace eyecandy
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.logMessages = new System.Windows.Forms.TextBox();
            this.buttonChangeWallpaper = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // notifyIcon
            // 
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "EyeCandy";
            this.notifyIcon.Visible = true;
            this.notifyIcon.Click += new System.EventHandler(this.notifyIcon_DoubleClick);
            // 
            // logMessages
            // 
            this.logMessages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logMessages.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.logMessages.Location = new System.Drawing.Point(12, 52);
            this.logMessages.Multiline = true;
            this.logMessages.Name = "logMessages";
            this.logMessages.ReadOnly = true;
            this.logMessages.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.logMessages.Size = new System.Drawing.Size(758, 189);
            this.logMessages.TabIndex = 0;
            this.logMessages.WordWrap = false;
            // 
            // buttonChangeWallpaper
            // 
            this.buttonChangeWallpaper.Location = new System.Drawing.Point(12, 13);
            this.buttonChangeWallpaper.Name = "buttonChangeWallpaper";
            this.buttonChangeWallpaper.Size = new System.Drawing.Size(142, 33);
            this.buttonChangeWallpaper.TabIndex = 1;
            this.buttonChangeWallpaper.Text = "change wallpaper";
            this.buttonChangeWallpaper.UseVisualStyleBackColor = true;
            this.buttonChangeWallpaper.Click += new System.EventHandler(this.buttonChangeWallpaper_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(161, 13);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(106, 33);
            this.button1.TabIndex = 2;
            this.button1.Text = "hide window";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(782, 253);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.buttonChangeWallpaper);
            this.Controls.Add(this.logMessages);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "EyeCandy";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.TextBox logMessages;
        private System.Windows.Forms.Button buttonChangeWallpaper;
        private System.Windows.Forms.Button button1;
    }
}

