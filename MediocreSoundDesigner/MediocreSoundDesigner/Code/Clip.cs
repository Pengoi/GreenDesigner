namespace MediocreSoundDesigner;

// One placed copy of a lane's sound on the timeline.
// Positions and lengths are counted in timeline 
// so they stay put when you change the zoom.
public class Clip {
  // where the clip begins on the timeline
  public int StartSample { get; set; }
  // how much of the sound it plays 
  public int LengthSamples { get; set; }
}
