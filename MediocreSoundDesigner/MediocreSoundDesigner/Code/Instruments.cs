namespace MediocreSoundDesigner;

// The default sounds on the instrument palette. Every sound is synthesized in code,
// so there are no audio files to ship.
public enum Instrument {
  Kick,
  Snare,
  Clap,
  HiHat,
  Cowbell,
  Bass,
  Synth,
  Bell,
}

public static class InstrumentSounds {
  private const int SampleRate = 44100;   // frames per second
  private const int Channels = 2;         // interleaved L/R, same as the lane timeline

  private static readonly double[] ClapBursts = { 0, 0.011, 0.022 };
  private static readonly double[] SynthDetune = { 0.993, 1.0, 1.007 };

  public static string DisplayName(Instrument instrument) {
    return instrument == Instrument.HiHat ? "Hi-Hat" : instrument.ToString();
  }

  public static float[] Generate(Instrument instrument) {
    return instrument switch {
      Instrument.Kick => Kick(),
      Instrument.Snare => Snare(),
      Instrument.Clap => Clap(),
      Instrument.HiHat => HiHat(),
      Instrument.Cowbell => Cowbell(),
      Instrument.Bass => Bass(),
      Instrument.Synth => Synth(),
      Instrument.Bell => Bell(),
      _ => new float[0],
    };
  }

  // ---- drums -----------------------------------------------------------------

  // a sine that sweeps quickly from high to low and rings out
  private static float[] Kick() {
    double phase = 0;
    return Render(0.7, t => {
      double freq = 42 + (120 * Math.Exp(-t * 28));
      phase += 2 * Math.PI * freq / SampleRate;
      return Math.Sin(phase) * Math.Exp(-t * 5.5) * Math.Min(1.0, t / 0.002) * 0.9;
    });
  }

  // noise plus a short tonal body
  private static float[] Snare() {
    Random rand = new(1);
    return Render(0.5, t => {
      double body = (Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-t * 22) * 0.35)
                  + (Math.Sin(2 * Math.PI * 330 * t) * Math.Exp(-t * 28) * 0.15);
      return ((Noise(rand) * Math.Exp(-t * 12) * 0.65) + body) * 0.9;
    });
  }

  // a few quick noise bursts followed by a short tail
  private static float[] Clap() {
    Random rand = new(3);
    return Render(0.5, t => {
      double env = 0;
      foreach (double start in ClapBursts) {
        if (t >= start) {
          env += Math.Exp(-(t - start) * 120) * 0.45;
        }
      }
      if (t >= 0.033) {
        env += Math.Exp(-(t - 0.033) * 12) * 0.5;
      }
      return Noise(rand) * Math.Min(1.0, env) * 0.8;
    });
  }

  // bright noise; each sample minus the one before it keeps the highs
  private static float[] HiHat() {
    Random rand = new(2);
    double prev = 0;
    return Render(0.35, t => {
      double noise = Noise(rand);
      double value = (noise - prev) * 0.5 * Math.Exp(-t * 16);
      prev = noise;
      return value;
    });
  }

  // two square waves a fifth apart that ring out, the classic cowbell sound
  private static float[] Cowbell() {
    return Render(0.5, t => {
      double a = Math.Sin(2 * Math.PI * 540 * t) >= 0 ? 1.0 : -1.0;
      double b = Math.Sin(2 * Math.PI * 800 * t) >= 0 ? 1.0 : -1.0;
      double env = (0.7 * Math.Exp(-t * 30)) + (0.3 * Math.Exp(-t * 6));
      return (a + b) * 0.5 * env * Math.Min(1.0, t / 0.001) * 0.55;
    });
  }

  // ---- melodic ---------------------------------------------------------------

  // a deep note (A1) with a few harmonics, softly clipped for warmth
  private static float[] Bass() {
    return Render(0.9, t => {
      double w = 2 * Math.PI * 55 * t;
      double v = Math.Sin(w) + (0.4 * Math.Sin(2 * w)) + (0.2 * Math.Sin(3 * w));
      return Math.Tanh(1.4 * v) * Math.Exp(-t * 2.5) * Math.Min(1.0, t / 0.005) * 0.6;
    });
  }

  // a held lead note (E4): three detuned saw waves, a little vibrato, and a filter that opens up
  private static float[] Synth() {
    double[] phases = new double[SynthDetune.Length];
    double y = 0;
    return Render(0.9, t => {
      double vibrato = 1 + (0.004 * Math.Sin(2 * Math.PI * 6 * t) * Math.Min(1.0, t / 0.2));
      double saw = 0;
      for (int k = 0; k < phases.Length; k++) {
        phases[k] = (phases[k] + (330 * SynthDetune[k] * vibrato / SampleRate)) % 1.0;
        saw += 2 * (phases[k] - 0.5);
      }
      saw /= phases.Length;
      double alpha = 0.12 + (0.25 * Math.Min(1.0, t / 0.25));
      y += alpha * (saw - y);
      double env = Math.Min(1.0, t / 0.015) * (0.55 + (0.45 * Math.Exp(-t * 3)));
      return y * env * 0.8;
    });
  }

  // FM synthesis: a high note modulated by an off-pitch tone gives a bell ring (C5)
  private static float[] Bell() {
    const double CARRIER = 523.25;
    return Render(1.5, t => {
      double index = 4.0 * Math.Exp(-t * 3);
      double v = Math.Sin((2 * Math.PI * CARRIER * t) + (index * Math.Sin(2 * Math.PI * CARRIER * 3.5 * t)));
      return v * Math.Exp(-t * 2.2) * Math.Min(1.0, t / 0.002) * 0.6;
    });
  }

  // ---- helpers ---------------------------------------------------------------

  // builds the sound one frame at a time, with a short fade at the end so it never clicks
  private static float[] Render(double seconds, Func<double, double> voice) {
    const double END_FADE = 0.02;
    int frames = (int)(SampleRate * seconds);
    float[] samples = new float[frames * Channels];
    for (int i = 0; i < frames; i++) {
      double t = (double)i / SampleRate;
      double fade = Math.Min(1.0, (seconds - t) / END_FADE);
      Write(samples, i, voice(t) * fade);
    }
    return samples;
  }

  private static double Noise(Random rand) {
    return (rand.NextDouble() * 2) - 1;
  }

  private static void Write(float[] samples, int frame, double value) {
    float v = (float)Math.Clamp(value, -1.0, 1.0);
    for (int c = 0; c < Channels; c++) {
      samples[(frame * Channels) + c] = v;
    }
  }
}
