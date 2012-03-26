//History
//==========================================================================================================
// 20120307 |  2.1.1   | Nino Liu   |  Cancel comment out of thisUSB.ChipReset() and remove gui top's vn.
//==========================================================================================================
namespace Dongle_Test_Suite_2._1
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.RunButton = new System.Windows.Forms.Button();
            this.StatusIndicator = new System.Windows.Forms.Panel();
            this.ScannerInputBox = new System.Windows.Forms.TextBox();
            this.Output = new System.Windows.Forms.Label();
            this.progressBar_detail = new Dongle_Test_Suite_2._1.ContinuousProgressBar();
            this.progressBar_overall = new Dongle_Test_Suite_2._1.ContinuousProgressBar();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.SuspendLayout();
            // 
            // RunButton
            // 
            this.RunButton.Location = new System.Drawing.Point(96, 169);
            this.RunButton.Name = "RunButton";
            this.RunButton.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.RunButton.Size = new System.Drawing.Size(92, 41);
            this.RunButton.TabIndex = 1;
            this.RunButton.Text = "Test and Load";
            this.RunButton.UseVisualStyleBackColor = true;
            this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
            // 
            // StatusIndicator
            // 
            this.StatusIndicator.BackColor = System.Drawing.Color.White;
            this.StatusIndicator.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.StatusIndicator.Location = new System.Drawing.Point(65, 263);
            this.StatusIndicator.Name = "StatusIndicator";
            this.StatusIndicator.Size = new System.Drawing.Size(150, 52);
            this.StatusIndicator.TabIndex = 2;
            // 
            // ScannerInputBox
            // 
            this.ScannerInputBox.Location = new System.Drawing.Point(35, 143);
            this.ScannerInputBox.Name = "ScannerInputBox";
            this.ScannerInputBox.Size = new System.Drawing.Size(212, 20);
            this.ScannerInputBox.TabIndex = 0;
            // 
            // Output
            // 
            this.Output.BackColor = System.Drawing.Color.White;
            this.Output.Location = new System.Drawing.Point(35, 18);
            this.Output.Name = "Output";
            this.Output.Size = new System.Drawing.Size(212, 113);
            this.Output.TabIndex = 3;
            // 
            // progressBar_detail
            // 
            this.progressBar_detail.Location = new System.Drawing.Point(35, 223);
            this.progressBar_detail.Name = "progressBar_detail";
            this.progressBar_detail.Size = new System.Drawing.Size(211, 14);
            this.progressBar_detail.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar_detail.TabIndex = 4;
            // 
            // progressBar_overall
            // 
            this.progressBar_overall.Location = new System.Drawing.Point(36, 243);
            this.progressBar_overall.Name = "progressBar_overall";
            this.progressBar_overall.Size = new System.Drawing.Size(211, 14);
            this.progressBar_overall.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar_overall.TabIndex = 5;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // MainForm
            // 
            this.AcceptButton = this.RunButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 328);
            this.Controls.Add(this.progressBar_overall);
            this.Controls.Add(this.progressBar_detail);
            this.Controls.Add(this.Output);
            this.Controls.Add(this.ScannerInputBox);
            this.Controls.Add(this.StatusIndicator);
            this.Controls.Add(this.RunButton);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "ThinkEco USB Test";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button RunButton;
        private System.Windows.Forms.Panel StatusIndicator;
        private System.Windows.Forms.TextBox ScannerInputBox;
        private System.Windows.Forms.Label Output;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private ContinuousProgressBar progressBar_detail;
        private ContinuousProgressBar progressBar_overall;
    }
}

