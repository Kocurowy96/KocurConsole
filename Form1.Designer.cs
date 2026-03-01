namespace KocurConsole
{
    partial class Form1
    {
        /// <summary>
        /// Wymagana zmienna projektanta.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Wyczyść wszystkie używane zasoby.
        /// </summary>
        /// <param name="disposing">prawda, jeżeli zarządzane zasoby powinny zostać zlikwidowane; Fałsz w przeciwnym wypadku.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Kod generowany przez Projektanta formularzy systemu Windows

        /// <summary>
        /// Metoda wymagana do obsługi projektanta — nie należy modyfikować
        /// jej zawartości w edytorze kodu.
        /// </summary>
        private void InitializeComponent()
        {
            this.richTextBoxConsoleOutput = new System.Windows.Forms.RichTextBox();
            this.inputCommands = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // richTextBoxConsoleOutput
            // 
            this.richTextBoxConsoleOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxConsoleOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxConsoleOutput.DetectUrls = false;
            this.richTextBoxConsoleOutput.HideSelection = false;
            this.richTextBoxConsoleOutput.Location = new System.Drawing.Point(13, 13);
            this.richTextBoxConsoleOutput.Name = "richTextBoxConsoleOutput";
            this.richTextBoxConsoleOutput.ReadOnly = true;
            this.richTextBoxConsoleOutput.Size = new System.Drawing.Size(775, 393);
            this.richTextBoxConsoleOutput.TabIndex = 0;
            this.richTextBoxConsoleOutput.Text = "";
            this.richTextBoxConsoleOutput.TextChanged += new System.EventHandler(this.richTextBoxConsoleOutput_TextChanged);
            // 
            // inputCommands
            // 
            this.inputCommands.AcceptsReturn = true;
            this.inputCommands.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.inputCommands.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.inputCommands.Location = new System.Drawing.Point(0, 430);
            this.inputCommands.Name = "inputCommands";
            this.inputCommands.Size = new System.Drawing.Size(800, 20);
            this.inputCommands.TabIndex = 1;
            this.inputCommands.Visible = false;
            this.inputCommands.KeyDown += new System.Windows.Forms.KeyEventHandler(this.inputCommands_KeyDown);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.inputCommands);
            this.Controls.Add(this.richTextBoxConsoleOutput);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBoxConsoleOutput;
        private System.Windows.Forms.TextBox inputCommands;
    }
}

