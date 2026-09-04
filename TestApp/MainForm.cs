using System;
using System.IO;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NAudio.Vorbis.TestApp;

/// <summary>
/// A small harness for playing an Ogg Vorbis file through NAudio, mainly to exercise
/// repositioning by hand: the position bar tracks playback and dragging it seeks.
/// </summary>
public partial class MainForm : Form
{
    private const int PositionSteps = 1000;

    private WasapiPlayer? player;
    private PlaybackSource? source;
    private bool updatingPositionBar;

    public MainForm()
    {
        InitializeComponent();
        UpdateControlStates();
    }

    private void OnOpenClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "Ogg Vorbis files (*.ogg)|*.ogg|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        Unload();
        try
        {
            source = new PlaybackSource(new VorbisWaveReader(dialog.FileName));
            Text = $"NAudio.Vorbis test harness - {Path.GetFileName(dialog.FileName)}";
            streamInfoTextBox.Text = source.DescribeStream();
            positionTrackBar.Value = 0;
            SetStatus($"Loaded {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            Unload();
            SetStatus($"Could not open the file: {ex.Message}", isError: true);
        }
        UpdateControlStates();
    }

    private void OnPlayClick(object? sender, EventArgs e)
    {
        if (source == null) return;

        try
        {
            // if we are sitting at the end (the file played out), start over
            if (source.Position >= source.Length) SeekTo(0);

            if (player == null)
            {
                player = new WasapiPlayerBuilder().Build();
                player.PlaybackStopped += OnPlaybackStopped;
                player.Init(source);
                player.Volume = volumeTrackBar.Value / 100f;
            }
            player.Play();
            positionTimer.Start();
            SetStatus("Playing");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not start playback: {ex.Message}", isError: true);
        }
        UpdateControlStates();
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        player?.Pause();
        positionTimer.Stop();
        SetStatus("Paused");
        UpdateControlStates();
    }

    private void OnStopClick(object? sender, EventArgs e)
    {
        player?.Stop();
        positionTimer.Stop();
        SeekTo(0);
        SetStatus("Stopped");
        UpdateControlStates();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPlaybackStopped(sender, e));
            return;
        }

        positionTimer.Stop();
        SetStatus(e.Exception == null ? "Reached the end of the file" : $"Playback stopped: {e.Exception.Message}",
                  isError: e.Exception != null);
        UpdateControlStates();
    }

    /// <summary>Dragging the position bar seeks; the timer writes to it while playing.</summary>
    private void OnPositionScroll(object? sender, EventArgs e)
    {
        if (updatingPositionBar || source == null) return;
        SeekTo(source.Length * positionTrackBar.Value / PositionSteps);
    }

    private void SeekTo(long bytePosition)
    {
        if (source == null) return;

        try
        {
            source.Seek(bytePosition);
            SetStatus($"Position {source.CurrentTime:mm\\:ss\\.fff} of {source.TotalTime:mm\\:ss\\.fff}");
        }
        catch (Exception ex)
        {
            // Seeking is the least reliable part of the decoder: NVorbis 0.10.5 can throw
            // "GranulePos mismatch" seeking into the final page, and there are files it
            // cannot seek back to the start of. Report it and carry on rather than dying -
            // seeing which files misbehave is the point of this harness.
            SetStatus($"Seek failed: {ex.GetType().Name}: {ex.Message}", isError: true);
        }
        UpdatePositionDisplay();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e) => UpdatePositionDisplay();

    private void UpdatePositionDisplay()
    {
        if (source == null) return;

        var position = source.Position;
        updatingPositionBar = true;
        try
        {
            positionTrackBar.Value = source.Length > 0
                ? (int)Math.Clamp(position * PositionSteps / source.Length, 0, PositionSteps)
                : 0;
        }
        finally
        {
            updatingPositionBar = false;
        }

        positionLabel.Text = $"{source.CurrentTime:mm\\:ss\\.fff} / {source.TotalTime:mm\\:ss\\.fff}"
                           + $"   ({position:N0} / {source.Length:N0} bytes)";
    }

    private void OnVolumeScroll(object? sender, EventArgs e)
    {
        if (player != null) player.Volume = volumeTrackBar.Value / 100f;
    }

    private void SetStatus(string message, bool isError = false)
    {
        statusLabel.Text = message;
        statusLabel.ForeColor = isError ? System.Drawing.Color.Firebrick : System.Drawing.SystemColors.ControlText;
    }

    private void UpdateControlStates()
    {
        var loaded = source != null;
        var playing = player?.PlaybackState == PlaybackState.Playing;

        playButton.Enabled = loaded && !playing;
        pauseButton.Enabled = loaded && playing;
        stopButton.Enabled = loaded && player != null && player.PlaybackState != PlaybackState.Stopped;
        positionTrackBar.Enabled = loaded;
    }

    private void Unload()
    {
        positionTimer.Stop();
        if (player != null)
        {
            player.PlaybackStopped -= OnPlaybackStopped;
            player.Dispose();
            player = null;
        }
        source?.Dispose();
        source = null;
        streamInfoTextBox.Clear();
        positionLabel.Text = "--:--.--- / --:--.---";
        Text = "NAudio.Vorbis test harness";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Unload();
        base.OnFormClosed(e);
    }
}
