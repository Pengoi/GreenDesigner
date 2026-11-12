using NAudio.Wave;

namespace MediocreSoundDesigner; 

public class TrackManager {
  private Track pseudoCombinedTrack;
  private Panel panContainer;

  public List<Track> Tracks { get; private set; }
	public Track SelectedTrack { get; private set; }
  public int DrawTimescale { get; set; }
  public PlaybackState State => pseudoCombinedTrack?.State ?? PlaybackState.Stopped;

  public TrackManager(Panel panContainer) {
		Tracks = new();
    SelectedTrack = null;
    pseudoCombinedTrack = null;
    this.panContainer = panContainer;
  }

	public void AddTrack(Track track) {
		Tracks.Add(track);
	}

    public bool MoveTrack(Track track, int newIndex) {
        int oldIndex = Tracks.IndexOf(track);
        if (oldIndex < 0 || oldIndex == newIndex) return false;
        Tracks.RemoveAt(oldIndex);
        Tracks.Insert(newIndex, track);
        return true;
    }

    public void RemTrack(Track track) {
		Tracks.Remove(track);
    if (SelectedTrack == track) {
      // don't leave a removed track selected
      SelectedTrack = null;
      if (Tracks.Count > 0) {
        SelectTrack(Tracks[^1]);
      }
    }
	}

	public void SelectTrack(Track track) {
		SelectedTrack = track;
    Tracks.ForEach(track => {
      track.PanDrawSamples.BorderStyle = BorderStyle.None;
      track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Unselected;
      track.PanHeader?.Invalidate();
    });
    foreach (Track other in Tracks) {
      if (other != track) {
        other.SelectedClip = null;   // only the selected lane keeps a selected clip
      }
    }
    track.PanDrawSamples.BorderStyle = BorderStyle.Fixed3D;
    track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Selected;
    track.PanHeader?.Invalidate();
  }

  public void RedrawAll() {
    Tracks.ForEach(track => {
      track.PanDrawSamples.Invalidate();
      track.PanHeader?.Invalidate();
    });
  }

  public void ApplySettings() {
    foreach (Track track in Tracks) {
      if (SelectedTrack == track) {
        track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Selected;
      }
      else {
        track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Unselected;
      }
    }
    RedrawAll();
  }

  public void SaveToFile(string filename) {
    CombineTracks();
    pseudoCombinedTrack.SaveToFile(filename);
  }

	public void PlayAll() {
    CombineTracks();
    pseudoCombinedTrack.Play();
  }
  private void CombineTracks() {
    pseudoCombinedTrack?.Dispose();
    List<float> combinedSamples = new();
    foreach (Track track in Tracks) {
      if (track.IsDisabled) {
        continue;
      }
      track.SetDrawTimescale(DrawTimescale);
      float[] trackSamples = (float[])track.GetAdjustedSamples().Clone();
      for (int i = 0; i < trackSamples.Length; i++) {
        if (combinedSamples.Count <= i) {
          combinedSamples.Add(0);
        }
        combinedSamples[i] += trackSamples[i];
      }
    }
    pseudoCombinedTrack = new(combinedSamples.ToArray());
    pseudoCombinedTrack.LinkPanels(null, panContainer);
  }
  public void PauseAll() {
    pseudoCombinedTrack?.Pause();
  }
  public int GetPlayheadPos() {
    return (pseudoCombinedTrack?.GetPlayheadPos() ?? 0) / DrawTimescale;
  }
}
