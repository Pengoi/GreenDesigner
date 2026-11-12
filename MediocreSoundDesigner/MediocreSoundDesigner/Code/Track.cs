using NAudio.Wave;

namespace MediocreSoundDesigner;

public enum SampleRegion {
  BEGINNING_ONLY,
  END_ONLY,
  BEGINNING_AND_END,
  ANYWHERE,
}

public class Track : IDisposable {
  // Fields
  private string file;
  private TimeSpan duration;
  private float offset;
  private float timeScale;
  private int sampleRate;
  private float[] samples;
  private float pitch;
  private int bits;
  private int channels;
  private AudioFileReader fileReader;
  private WaveOut outputDevice;
  private Panel panContainer;

 
  public const int TimelineRate = 44100 * 2;
  public const int GridStepsPerSecond = 4;
  public const int GridSamples = TimelineRate / GridStepsPerSecond;   // clips snap to every quarter second

  // properties
  public List<Clip> Clips { get; } = new();
  public Clip SelectedClip { get; set; }
  public bool IsDisabled { get; private set; }
  public Panel PanDrawSamples { get; private set; }
  public Panel PanHeader { get; private set; }
  public string Name { get; set; } = "";
  public float Volume { get; private set; }
  private string tempFilePath => Path.Combine(Path.GetTempPath(), "MediocreSoundDesigner", "temp.wav");
  public PlaybackState State => outputDevice?.PlaybackState ?? PlaybackState.Stopped;

  // ctor
  public Track(string file) {
    fileReader = new AudioFileReader(file);
    int sampleCount = (int)(fileReader.Length / 4);
    this.samples = new float[sampleCount];
    int samplesRead = ((ISampleProvider)fileReader).Read(samples);
    if (samplesRead < sampleCount) {
      Array.Resize(ref samples, samplesRead);
    }

    this.file = file;
    this.duration = fileReader.TotalTime;
    this.offset = 0;
    this.timeScale = 1.0f;
    this.Volume = 1.0f;
    this.pitch = 1.0f;
    this.bits = fileReader.WaveFormat.BitsPerSample;
    this.channels = fileReader.WaveFormat.Channels;
    this.sampleRate = fileReader.WaveFormat.SampleRate;
    IsDisabled = false;
    ResetClips();
  }
  public Track(float[] samples) {
    this.samples = samples;

    this.file = "";
    this.duration = TimeSpan.Zero;
    this.offset = 0;
    this.timeScale = 1.0f;
    this.Volume = 1.0f;
    this.pitch = 1.0f;
    this.bits = 32;
    this.channels = 1;
    this.sampleRate = 0;
    IsDisabled = false;
    ResetClips();
  }
  public void LinkPanels(Panel panDrawSamples, Panel panContainer) {
    this.PanDrawSamples = panDrawSamples;
    this.panContainer = panContainer;
  }
  public void LinkHeader(Panel panHeader) {
    this.PanHeader = panHeader;
  }

  // edits
  public float[] CloneSamples() {
    return (float[])samples.Clone();
  }
  public void ReplaceSamples(float[] samples) {
    this.samples = samples;
    ResetClips();
  }
  public void Reverse() {
    for (int i = 0; i < samples.Length / 2; i++) {
      var temp = samples[i];
      samples[i] = samples[samples.Length - 1 - i];
      samples[samples.Length - 1 - i] = temp;
    }
  }
  public void Flip() {
    for (int i = 0; i < samples.Length; i++) {
      samples[i] *= -1;
    }
  }
  public void Trim() {
  }
  public void AlterTimeScale(float scale) {
  }
  public void SetVolume(float newVolume) {
    Volume = newVolume;
  }
  public void EliminateSilence(SampleRegion region, float minDuration, float thres) {
    float[] newSamples = CloneSamples();
    if (region is SampleRegion.BEGINNING_ONLY or SampleRegion.BEGINNING_AND_END) {
      int silentDurationInNumSamples = 0;
      for (int i = 0; i < newSamples.Length; i++) {
        if (Math.Abs(newSamples[i]) <= thres) {
          silentDurationInNumSamples++;
        }
        else {
          break;
        }
      }
      if ((float)silentDurationInNumSamples / sampleRate >= minDuration) {
        newSamples = newSamples[silentDurationInNumSamples..];
      }
    }
    if (region is SampleRegion.END_ONLY or SampleRegion.BEGINNING_AND_END) {
      int silentDurationInNumSamples = 0;
      for (int i = newSamples.Length - 1; i >= 0; i--) {
        if (Math.Abs(newSamples[i]) <= thres) {
          silentDurationInNumSamples++;
        }
        else {
          break;
        }
      }
      if ((float)silentDurationInNumSamples / sampleRate >= minDuration) {
        newSamples = newSamples[0..^silentDurationInNumSamples];
      }
    }
    if (region is SampleRegion.ANYWHERE) {
      int silentDurationInNumSamples = 0;
      int silentRegionStart = 0;
      int i;
      for (i = 0; i < newSamples.Length; i++) {
        if (Math.Abs(newSamples[i]) <= thres) {
          silentDurationInNumSamples++;
        }
        else {
          if ((float)silentDurationInNumSamples / sampleRate >= minDuration) {
            newSamples = [..newSamples[0..silentRegionStart], ..newSamples[i..]];
            i = 0;
          }
          silentRegionStart = i + 1;
          silentDurationInNumSamples = 0;
        }
      }
      if ((float)silentDurationInNumSamples / sampleRate >= minDuration) {
        newSamples = [.. newSamples[0..silentRegionStart], .. newSamples[i..]];
      }
    }
    samples = newSamples;
  }

  // play
  public void Play() {
    if (samples.Length == 0) {
      return;   // nothing to play 
    }
    outputDevice?.Dispose();
    fileReader?.Dispose();
    SaveToTempFile();
    fileReader = new AudioFileReader(tempFilePath);
    outputDevice = new WaveOut();
    outputDevice.Init(fileReader);
    outputDevice.Play();
  }
  public void Pause() {
    outputDevice?.Pause();
  }
  public void Reset() {
  }
  public int GetPlayheadPos() {
    return (int)((outputDevice?.GetPosition() ?? 0) / 4);
  }
  public void ToggleDisabled() {
    IsDisabled = !IsDisabled;
  }

  // file i/o
  private void SaveToTempFile() {
    Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));
    float[] adjSamples = GetAdjustedSamples();
    using var writer = new WaveFileWriter(tempFilePath, fileReader?.WaveFormat ?? new());
    writer.WriteSamples(adjSamples, 0, adjSamples.Length);
  }
  public void SaveToFile(string path) {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    float[] adjSamples = GetAdjustedSamples();
    using var writer = new WaveFileWriter(path, fileReader?.WaveFormat ?? new());
    writer.WriteSamples(adjSamples, 0, adjSamples.Length);
  }
  // Mixes every clip on this lane 
  public float[] GetAdjustedSamples() {
    int end = 0;
    foreach (Clip clip in Clips) {
      end = Math.Max(end, clip.StartSample + EffectiveLength(clip));
    }
    float[] adjSamples = new float[end];
    foreach (Clip clip in Clips) {
      int length = EffectiveLength(clip);
      for (int s = 0; s < length; s++) {
        adjSamples[clip.StartSample + s] += samples[s] * Volume;
      }
    }
    return adjSamples;
  }

  // clips
  // how much of the sound a clip plays 
  public int EffectiveLength(Clip clip) {
    return Math.Clamp(clip.LengthSamples, 0, samples.Length);
  }
  // back to a single clip covering the whole sound
  private void ResetClips() {
    Clips.Clear();
    SelectedClip = null;
    Clips.Add(new Clip { StartSample = 0, LengthSamples = samples.Length });
  }
  public static int SnapToGrid(int sampleIndex) {
    return (int)Math.Round((double)sampleIndex / GridSamples) * GridSamples;
  }
  // drops another full-length copy of the sound at startSample 
  public Clip AddClipAt(int startSample) {
    if (samples is null || samples.Length == 0) {
      return null;
    }
    Clip clip = new() { StartSample = Math.Max(0, startSample), LengthSamples = samples.Length };
    Clips.Add(clip);
    return clip;
  }
  public void RemoveClip(Clip clip) {
    Clips.Remove(clip);
    if (SelectedClip == clip) {
      SelectedClip = null;
    }
  }
  // trims or lengthens a clip
  public void ResizeClip(Clip clip, int newLength) {
    int max = samples.Length;
    int min = Math.Min(GridSamples, max);
    if (max - newLength < GridSamples / 2) {
      newLength = max;   // close to the original length
    }
    clip.LengthSamples = Math.Clamp(newLength, min, max);
  }
  public void NudgeSelectedClip(int gridSteps) {
    if (SelectedClip is null) {
      return;
    }
    SelectedClip.StartSample = Math.Max(0, SelectedClip.StartSample + gridSteps * GridSamples);
  }
  // finds the topmost clip under a pixel
  public Clip FindClipAt(int px, int drawTimescale, int edgePx, out bool onRightEdge) {
    onRightEdge = false;
    for (int i = Clips.Count - 1; i >= 0; i--) {
      Clip clip = Clips[i];
      int length = EffectiveLength(clip);
      if (length <= 0) {
        continue;
      }
      int x0 = clip.StartSample / drawTimescale;
      int x1 = (clip.StartSample + length) / drawTimescale;
      if (px >= x0 && px <= x1) {
        onRightEdge = px >= x1 - edgePx;
        return clip;
      }
    }
    return null;
  }

  // render
  public void DrawSamples(Graphics g, int drawTimescale) {
    if (PanDrawSamples is null) {
      return;
    }
    int width = PanDrawSamples.ClientSize.Width;
    int height = PanDrawSamples.ClientSize.Height;
    if (width <= 0 || height <= 0) {
      return;
    }
    DrawGrid(g, drawTimescale, width, height);
    foreach (Clip clip in Clips) {
      DrawClip(g, clip, drawTimescale, width, height);
    }
  }
  // vertical grid lines every quarter second, stronger on each whole second
  private void DrawGrid(Graphics g, int drawTimescale, int width, int height) {
    float stepPx = (float)GridSamples / drawTimescale;
    if (stepPx < 4) {
      return;   // too tightly packed to be useful
    }
    using Pen fine = new(Settings.Color.Grid_Line);
    using Pen bar = new(Settings.Color.Grid_Bar);
    for (int n = 0; n * stepPx < width; n++) {
      float x = n * stepPx;
      g.DrawLine(n % GridStepsPerSecond == 0 ? bar : fine, x, 0, x, height);
    }
  }
  private void DrawClip(Graphics g, Clip clip, int drawTimescale, int width, int height) {
    int length = EffectiveLength(clip);
    if (length <= 0) {
      return;
    }
    int x0 = clip.StartSample / drawTimescale;              // left edge in pixels
    int x1 = (clip.StartSample + length) / drawTimescale;   // right edge in pixels
    int visLeft = Math.Max(0, x0);
    int visRight = Math.Min(width, x1);
    if (visRight <= visLeft) {
      return;
    }
    bool selected = clip == SelectedClip;

    using (SolidBrush fill = new(IsDisabled ? Settings.Color.Clip_Fill_Disabled : Settings.Color.Clip_Fill)) {
      g.FillRectangle(fill, visLeft, 0, visRight - visLeft, height);
    }

    // waveform: one vertical line per pixel, from the quietest to the loudest sample under it
    float mid = height / 2f;
    float scale = (height / 2f) * Volume;
    using (Pen wave = new(IsDisabled ? Settings.Color.TrackSamples_Disabled : Settings.Color.TrackSamples_Enabled)) {
      for (int px = visLeft; px < visRight; px++) {
        int first = (px - x0) * drawTimescale;
        int last = Math.Min(first + drawTimescale, length);
        if (first >= last) {
          continue;
        }
        float min = samples[first];
        float max = min;
        for (int s = first + 1; s < last; s++) {
          float v = samples[s];
          if (v < min) min = v;
          if (v > max) max = v;
        }
        g.DrawLine(wave, px, mid - max * scale, px, mid - min * scale + 1);
      }
    }

    // outline; the right edge is the handle for trimming
    using Pen border = new(selected ? Settings.Color.Clip_Border_Selected : Settings.Color.Clip_Border);
    g.DrawLine(border, visLeft, 0, visRight - 1, 0);
    g.DrawLine(border, visLeft, height - 1, visRight - 1, height - 1);
    if (x0 >= 0) {
      g.DrawLine(border, x0, 0, x0, height);
    }
    if (x1 <= width) {
      g.DrawLine(border, x1 - 1, 0, x1 - 1, height);
    }
  }

  // dispose
  public void Dispose() {
    outputDevice?.Dispose();
    fileReader?.Dispose();
  }
}
