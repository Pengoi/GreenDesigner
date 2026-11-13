namespace MediocreSoundDesigner;

// Layout for the top bar and the track name column.
public partial class FrmMain {
  private Panel panTopBar;
  private Panel panTimeline;
  private int nextTrackNumber = 1;

  private const int HEADER_WIDTH = 150;   // track name column
  private const int TIMESCALE_WIDTH = 320; // zoom slider
  private const int TIMELINE_HEIGHT = 32;

  private int HeaderWidth => LogicalToDeviceUnits(HEADER_WIDTH);

  
  private class BufferedPanel : Panel {
    public BufferedPanel() {
      DoubleBuffered = true;
    }
  }

  // A thick bar strip under the menu. For now it only holds the play button and the slider 
  private void BuildTopBar() {
    panTopBar = new BufferedPanel { Name = "panTopBar" };
    panTopBar.Paint += panTopBar_Paint;
    Controls.Add(panTopBar);
    panTopBar.Controls.Add(btnPlay);
    panTopBar.Controls.Add(trkDrawTimescale);
    panTopBar.Controls.Add(btnAutoFit);

    panTimeline = new BufferedPanel { Name = "panTimeline" };
    panTimeline.Paint += panTimeline_Paint;
    panTracks.Controls.Add(panTimeline);

    BuildInstruments();
  }

  private void ApplyTopBarSettings() {
    if (panTopBar is null) {
      return;
    }
    panTopBar.BackColor = Settings.Color.TopBar_Bg;
    panTimeline.BackColor = Settings.Color.TopBar_Bg;
    trkDrawTimescale.BackColor = Settings.Color.TopBar_Bg;
    ApplyInstrumentSettings();
    panTopBar.Invalidate();
    panTimeline.Invalidate();
  }

  private void panTopBar_Paint(object sender, PaintEventArgs e) {
    using Pen border = new(Settings.Color.TopBar_Border);
    e.Graphics.DrawRectangle(border, 0, 0, panTopBar.Width - 1, panTopBar.Height - 1);
  }

  private void panTimeline_Paint(object sender, PaintEventArgs e) {
    int headerWidth = HeaderWidth;
    int viewportWidth = Math.Max(1, panTimeline.Width - headerWidth);
    int pixelsPerSecond = Math.Max(1, Track.TimelineRate / Math.Max(1, trackManager.DrawTimescale));
    int firstSecond = Math.Max(0, trackManager.ScrollPixels / pixelsPerSecond);
    int lastSecond = firstSecond + (viewportWidth / pixelsPerSecond) + 2;

    e.Graphics.SetClip(new Rectangle(headerWidth, 0, viewportWidth, panTimeline.Height));
    e.Graphics.TranslateTransform(headerWidth - trackManager.ScrollPixels, 0);

    using Pen tickPen = new(Color.FromArgb(110, Color.Gray));
    using Pen majorPen = new(Color.FromArgb(180, Color.Gray));
    using Brush textBrush = new SolidBrush(Settings.Color.TrackHeader_Text);
    using Font font = new(Font.FontFamily, 8.0f, FontStyle.Regular, GraphicsUnit.Point);

    for (int second = firstSecond; second <= lastSecond; second++) {
      int x = second * pixelsPerSecond;
      e.Graphics.DrawLine(second == 0 ? majorPen : tickPen, x, 0, x, panTimeline.Height - 1);
      e.Graphics.DrawString(second.ToString(), font, textBrush, x + 4, 4);
    }

    using Pen borderPen = new(Settings.Color.TopBar_Border);
    e.Graphics.ResetTransform();
    e.Graphics.DrawLine(borderPen, 0, panTimeline.Height - 1, panTimeline.Width, panTimeline.Height - 1);
    e.Graphics.DrawLine(borderPen, headerWidth - 1, 0, headerWidth - 1, panTimeline.Height);
  }

  private void LayoutTopBar() {
    if (panTopBar is null) {
      return;
    }
    int gap = LogicalToDeviceUnits(6);
    int pad = LogicalToDeviceUnits(14);

    // line the bar up with the track area underneath it
    panTopBar.Left = panTracks.Left;
    panTopBar.Width = panTracks.Width;
    panTopBar.Top = menu.Bottom;
    panTopBar.Height = Math.Max(1, panTracks.Top - menu.Bottom - gap);

    panTimeline.Left = 0;
    panTimeline.Top = 0;
    panTimeline.Width = panTracks.Width;
    panTimeline.Height = LogicalToDeviceUnits(TIMELINE_HEIGHT);
    panTimeline.Invalidate();

    btnPlay.Left = pad;
    btnPlay.Top = Math.Max(0, (panTopBar.Height - btnPlay.Height) / 2);

    trkDrawTimescale.Left = btnPlay.Right + pad;
    trkDrawTimescale.Width = Math.Min(LogicalToDeviceUnits(TIMESCALE_WIDTH), Math.Max(1, panTopBar.Width - trkDrawTimescale.Left - pad));
    trkDrawTimescale.Top = Math.Max(0, (panTopBar.Height - trkDrawTimescale.Height) / 2);

    btnAutoFit.Left = trkDrawTimescale.Right + pad;
    btnAutoFit.Top = Math.Max(0, (panTopBar.Height - btnAutoFit.Height) / 2);

    LayoutInstruments(btnAutoFit.Right + pad);
  }

  // ---- track name column -----------------------------------------------------
  private void CreateTrackHeader(Track track) {
    if (string.IsNullOrWhiteSpace(track.Name)) {
      track.Name = $"Track {nextTrackNumber}";
      nextTrackNumber++;
    }

    TrackHeader header = new(track, () => trackManager.SelectedTrack == track);
    header.HeaderClicked += (sender, e) => {
      trackManager.SelectTrack(track);
    };
    header.MuteToggled += (sender, e) => {
      track.ToggleDisabled();
      track.PanDrawSamples.Invalidate();
      header.Invalidate();
    };
    // drag the name column up/down to reorder lanes
    header.MouseDown += (sender, e) => {
      if (e.Button != MouseButtons.Left) {
        return;
      }
      dragTrack = track;
      dragStart = Cursor.Position;
      dragging = false;
    };
    header.MouseMove += (sender, e) => {
      if (dragTrack != track || e.Button != MouseButtons.Left) {
        return;
      }
      Point cur = Cursor.Position;
      if (!dragging) {
        // ignore tiny wiggles so plain clicks (select / mute dot) still work
        if (Math.Abs(cur.Y - dragStart.Y) < DRAG_THRESHOLD) {
          return;
        }
        dragging = true;
        header.Cursor = Cursors.SizeNS;
      }
      DragTrackTo(track, cur);
    };
    header.MouseUp += (sender, e) => {
      dragTrack = null;
      dragging = false;
      header.Cursor = Cursors.Default;
    };
    panTracks.Controls.Add(header);
    track.LinkHeader(header);
  }

  private void RemoveHeader(Track track) {
    if (track.PanHeader is null) {
      return;
    }
    panTracks.Controls.Remove(track.PanHeader);
    track.PanHeader.Dispose();
  }
}
