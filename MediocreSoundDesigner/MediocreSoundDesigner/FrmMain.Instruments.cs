using System.Drawing.Drawing2D;

namespace MediocreSoundDesigner;

// The instrument palette in the top bar, to the right of the Auto-Fit button.
// Clicking an instrument adds a new lane holding that sound.
public partial class FrmMain {
  private const int INSTRUMENT_COLUMNS = 4;
  private const int INSTRUMENT_MIN_WIDTH = 260;   // hide the palette if the bar gets too narrow

  private Panel panInstruments;
  private Label lblInstruments;
  private Font instrumentFont;
  private readonly List<InstrumentPad> instrumentButtons = new();

  // each instrument gets its own color, like the channel rack in FL Studio
  private static Color InstrumentColor(Instrument instrument) {
    return instrument switch {
      Instrument.Kick => Color.FromArgb(232, 88, 64),
      Instrument.Snare => Color.FromArgb(240, 150, 56),
      Instrument.Clap => Color.FromArgb(232, 192, 64),
      Instrument.HiHat => Color.FromArgb(130, 200, 90),
      Instrument.Cowbell => Color.FromArgb(52, 190, 170),
      Instrument.Bass => Color.FromArgb(70, 140, 235),
      Instrument.Synth => Color.FromArgb(150, 100, 230),
      Instrument.Bell => Color.FromArgb(226, 100, 170),
      _ => Color.Gray,
    };
  }

  private void BuildInstruments() {
    instrumentFont = new Font(Font, FontStyle.Bold);
    panInstruments = new Panel { Name = "panInstruments" };
    lblInstruments = new Label {
      Text = "Instruments",
      AutoSize = true,
      Font = instrumentFont,
    };
    panInstruments.Controls.Add(lblInstruments);

    foreach (Instrument instrument in Enum.GetValues<Instrument>()) {
      Instrument chosen = instrument;
      InstrumentPad pad = new(InstrumentColor(instrument)) {
        Text = InstrumentSounds.DisplayName(instrument),
        Font = instrumentFont,
      };
      pad.Click += (sender, e) => AddInstrumentTrack(chosen);
      instrumentButtons.Add(pad);
      panInstruments.Controls.Add(pad);
    }
    panTopBar.Controls.Add(panInstruments);
  }

  private void ApplyInstrumentSettings() {
    if (panInstruments is null) {
      return;
    }
    panInstruments.BackColor = Settings.Color.TopBar_Bg;
    lblInstruments.ForeColor = Settings.Color.Clip_Border;
    foreach (InstrumentPad pad in instrumentButtons) {
      pad.Invalidate();
    }
  }

  // left = where the palette starts inside the top bar
  private void LayoutInstruments(int left) {
    if (panInstruments is null) {
      return;
    }
    int pad = LogicalToDeviceUnits(14);
    int gap = LogicalToDeviceUnits(8);
    int width = panTopBar.Width - left - pad;
    if (width < LogicalToDeviceUnits(INSTRUMENT_MIN_WIDTH)) {
      panInstruments.Visible = false;
      return;
    }
    panInstruments.Visible = true;

    int top = LogicalToDeviceUnits(10);
    panInstruments.SetBounds(left, top, width, Math.Max(1, panTopBar.Height - (top * 2)));
    lblInstruments.Location = new Point(0, 0);

    int rows = (instrumentButtons.Count + INSTRUMENT_COLUMNS - 1) / INSTRUMENT_COLUMNS;
    int gridTop = lblInstruments.Bottom + gap;
    int btnWidth = Math.Min(LogicalToDeviceUnits(120), (width - (gap * (INSTRUMENT_COLUMNS - 1))) / INSTRUMENT_COLUMNS);
    int btnHeight = Math.Min(LogicalToDeviceUnits(52), Math.Max(1, (panInstruments.Height - gridTop - (gap * (rows - 1))) / rows));
    for (int i = 0; i < instrumentButtons.Count; i++) {
      instrumentButtons[i].SetBounds(
        (i % INSTRUMENT_COLUMNS) * (btnWidth + gap),
        gridTop + ((i / INSTRUMENT_COLUMNS) * (btnHeight + gap)),
        btnWidth,
        btnHeight);
    }
  }

  private void AddInstrumentTrack(Instrument instrument) {
    Track newTrack = new(InstrumentSounds.Generate(instrument));
    newTrack.Name = InstrumentSounds.DisplayName(instrument);
    CreateTrackPanel(newTrack);
    trackManager.AddTrack(newTrack);
    UpdateSizes();
    newTrack.PanDrawSamples.Invalidate();
  }

  // A rounded pad with a gradient, a glowing LED dot and the instrument name.
  // Brightens when the mouse is over it and sinks in when pressed.
  private class InstrumentPad : Control {
    private readonly Color accent;
    private bool hover;
    private bool pressed;

    public InstrumentPad(Color accent) {
      this.accent = accent;
      SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, false);
      SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
      TabStop = false;
      Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) {
      base.OnMouseEnter(e);
      hover = true;
      Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e) {
      base.OnMouseLeave(e);
      hover = false;
      pressed = false;
      Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      if (e.Button == MouseButtons.Left) {
        pressed = true;
        Invalidate();
      }
    }
    protected override void OnMouseUp(MouseEventArgs e) {
      base.OnMouseUp(e);
      pressed = false;
      Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
      Graphics g = e.Graphics;
      g.Clear(Parent?.BackColor ?? BackColor);
      if (Width < 8 || Height < 8) {
        return;
      }
      g.SmoothingMode = SmoothingMode.AntiAlias;

      Rectangle r = new(1, 1, Width - 3, Height - 3);
      int radius = Math.Max(4, Math.Min(Width, Height) / 6);
      using GraphicsPath path = RoundedRect(r, radius);

      Color top = pressed ? Darken(accent, 0.35) : Lighten(accent, hover ? 0.28 : 0.14);
      Color bottom = Darken(accent, pressed ? 0.6 : 0.45);
      using (LinearGradientBrush fill = new(r, top, bottom, LinearGradientMode.Vertical)) {
        g.FillPath(fill, path);
      }
      using (Pen edge = new(Lighten(accent, hover ? 0.5 : 0.25))) {
        g.DrawPath(edge, path);
      }

      // LED dot on the left
      int led = Math.Max(6, Height / 5);
      Rectangle ledRect = new(r.Left + led, r.Top + ((r.Height - led) / 2), led, led);
      using (SolidBrush glow = new(Color.FromArgb(70, Lighten(accent, 0.6)))) {
        g.FillEllipse(glow, Rectangle.Inflate(ledRect, led / 3, led / 3));
      }
      using (SolidBrush dot = new(Lighten(accent, hover || pressed ? 0.8 : 0.6))) {
        g.FillEllipse(dot, ledRect);
      }

      // name
      int textLeft = ledRect.Right + (led / 2);
      Rectangle textRect = new(textLeft, r.Top + (pressed ? 1 : 0), Math.Max(1, r.Right - textLeft), r.Height);
      TextRenderer.DrawText(g, Text, Font, textRect, Color.White,
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius) {
      int d = radius * 2;
      GraphicsPath path = new();
      path.AddArc(r.Left, r.Top, d, d, 180, 90);
      path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
      path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
      path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
      path.CloseFigure();
      return path;
    }

    private static Color Lighten(Color c, double amount) {
      return Color.FromArgb(c.A, c.R + (int)((255 - c.R) * amount), c.G + (int)((255 - c.G) * amount), c.B + (int)((255 - c.B) * amount));
    }
    private static Color Darken(Color c, double amount) {
      return Color.FromArgb(c.A, (int)(c.R * (1 - amount)), (int)(c.G * (1 - amount)), (int)(c.B * (1 - amount)));
    }
  }
}
