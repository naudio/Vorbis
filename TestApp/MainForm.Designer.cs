namespace NAudio.Vorbis.TestApp;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private System.Windows.Forms.Button openButton;
    private System.Windows.Forms.Button playButton;
    private System.Windows.Forms.Button pauseButton;
    private System.Windows.Forms.Button stopButton;
    private System.Windows.Forms.TrackBar positionTrackBar;
    private System.Windows.Forms.Label positionLabel;
    private System.Windows.Forms.Label volumeCaptionLabel;
    private System.Windows.Forms.TrackBar volumeTrackBar;
    private System.Windows.Forms.TextBox streamInfoTextBox;
    private System.Windows.Forms.Label statusLabel;
    private System.Windows.Forms.Timer positionTimer;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        openButton = new System.Windows.Forms.Button();
        playButton = new System.Windows.Forms.Button();
        pauseButton = new System.Windows.Forms.Button();
        stopButton = new System.Windows.Forms.Button();
        positionTrackBar = new System.Windows.Forms.TrackBar();
        positionLabel = new System.Windows.Forms.Label();
        volumeCaptionLabel = new System.Windows.Forms.Label();
        volumeTrackBar = new System.Windows.Forms.TrackBar();
        streamInfoTextBox = new System.Windows.Forms.TextBox();
        statusLabel = new System.Windows.Forms.Label();
        positionTimer = new System.Windows.Forms.Timer(components);
        ((System.ComponentModel.ISupportInitialize)positionTrackBar).BeginInit();
        ((System.ComponentModel.ISupportInitialize)volumeTrackBar).BeginInit();
        SuspendLayout();

        openButton.Location = new System.Drawing.Point(12, 12);
        openButton.Name = "openButton";
        openButton.Size = new System.Drawing.Size(100, 30);
        openButton.TabIndex = 0;
        openButton.Text = "Open .ogg...";
        openButton.UseVisualStyleBackColor = true;
        openButton.Click += OnOpenClick;

        playButton.Location = new System.Drawing.Point(126, 12);
        playButton.Name = "playButton";
        playButton.Size = new System.Drawing.Size(80, 30);
        playButton.TabIndex = 1;
        playButton.Text = "Play";
        playButton.UseVisualStyleBackColor = true;
        playButton.Click += OnPlayClick;

        pauseButton.Location = new System.Drawing.Point(212, 12);
        pauseButton.Name = "pauseButton";
        pauseButton.Size = new System.Drawing.Size(80, 30);
        pauseButton.TabIndex = 2;
        pauseButton.Text = "Pause";
        pauseButton.UseVisualStyleBackColor = true;
        pauseButton.Click += OnPauseClick;

        stopButton.Location = new System.Drawing.Point(298, 12);
        stopButton.Name = "stopButton";
        stopButton.Size = new System.Drawing.Size(80, 30);
        stopButton.TabIndex = 3;
        stopButton.Text = "Stop";
        stopButton.UseVisualStyleBackColor = true;
        stopButton.Click += OnStopClick;

        volumeCaptionLabel.AutoSize = true;
        volumeCaptionLabel.Location = new System.Drawing.Point(400, 20);
        volumeCaptionLabel.Name = "volumeCaptionLabel";
        volumeCaptionLabel.Size = new System.Drawing.Size(50, 15);
        volumeCaptionLabel.TabIndex = 4;
        volumeCaptionLabel.Text = "Volume";

        volumeTrackBar.AutoSize = false;
        volumeTrackBar.Location = new System.Drawing.Point(456, 14);
        volumeTrackBar.Maximum = 100;
        volumeTrackBar.Name = "volumeTrackBar";
        volumeTrackBar.Size = new System.Drawing.Size(120, 30);
        volumeTrackBar.TabIndex = 5;
        volumeTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
        volumeTrackBar.Value = 100;
        volumeTrackBar.Scroll += OnVolumeScroll;

        positionTrackBar.AutoSize = false;
        positionTrackBar.Location = new System.Drawing.Point(12, 58);
        positionTrackBar.Maximum = 1000;
        positionTrackBar.Name = "positionTrackBar";
        positionTrackBar.Size = new System.Drawing.Size(564, 32);
        positionTrackBar.TabIndex = 6;
        positionTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
        positionTrackBar.Scroll += OnPositionScroll;

        positionLabel.AutoSize = true;
        positionLabel.Location = new System.Drawing.Point(12, 96);
        positionLabel.Name = "positionLabel";
        positionLabel.Size = new System.Drawing.Size(140, 15);
        positionLabel.TabIndex = 7;
        positionLabel.Text = "--:--.--- / --:--.---";

        streamInfoTextBox.Font = new System.Drawing.Font("Consolas", 9F);
        streamInfoTextBox.Location = new System.Drawing.Point(12, 120);
        streamInfoTextBox.Multiline = true;
        streamInfoTextBox.Name = "streamInfoTextBox";
        streamInfoTextBox.ReadOnly = true;
        streamInfoTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        streamInfoTextBox.Size = new System.Drawing.Size(564, 160);
        streamInfoTextBox.TabIndex = 8;

        statusLabel.AutoEllipsis = true;
        statusLabel.Location = new System.Drawing.Point(12, 290);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new System.Drawing.Size(564, 34);
        statusLabel.TabIndex = 9;
        statusLabel.Text = "Open an .ogg file to begin.";

        positionTimer.Interval = 50;
        positionTimer.Tick += OnPositionTimerTick;

        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(588, 332);
        Controls.Add(statusLabel);
        Controls.Add(streamInfoTextBox);
        Controls.Add(positionLabel);
        Controls.Add(positionTrackBar);
        Controls.Add(volumeTrackBar);
        Controls.Add(volumeCaptionLabel);
        Controls.Add(stopButton);
        Controls.Add(pauseButton);
        Controls.Add(playButton);
        Controls.Add(openButton);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "NAudio.Vorbis test harness";
        ((System.ComponentModel.ISupportInitialize)positionTrackBar).EndInit();
        ((System.ComponentModel.ISupportInitialize)volumeTrackBar).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
