namespace MediocreSoundDesigner;

// Mouse handling for the clips on a lane:
//   click empty space  -> drop another copy of the sound there
//   drag a clip        -> move it (snaps to the grid)
//   drag its right edge-> trim / lengthen it (snaps to the grid)
//   right-click a clip -> remove it
public partial class FrmMain {
  private enum ClipDrag {
    None,
    Move,
    Resize,
  }

  private void AttachClipMouse(Panel pan, Track track) {
    ClipDrag mode = ClipDrag.None;
    Clip dragClip = null;
    int grabOffset = 0;   // how far into the clip (in samples) the mouse grabbed it

    pan.MouseDown += (sender, e) => {
      trackManager.SelectTrack(track);
      int timescale = trackManager.DrawTimescale;
      int mouseSample = e.X * timescale;
      Clip hit = track.FindClipAt(e.X, timescale, LogicalToDeviceUnits(6), out bool onRightEdge);

      if (e.Button == MouseButtons.Right) {
        if (hit is not null) {
          track.RemoveClip(hit);
          pan.Invalidate();
        }
        return;
      }
      if (e.Button != MouseButtons.Left) {
        return;
      }

      if (hit is null) {
        // empty space: drop another copy of the sound here
        track.SelectedClip = track.AddClipAt(Track.SnapToGrid(mouseSample));
        pan.Invalidate();
        return;
      }

      track.SelectedClip = hit;
      dragClip = hit;
      if (onRightEdge) {
        mode = ClipDrag.Resize;
      }
      else {
        mode = ClipDrag.Move;
        grabOffset = mouseSample - hit.StartSample;
      }
      pan.Invalidate();
    };

    pan.MouseMove += (sender, e) => {
      int timescale = trackManager.DrawTimescale;
      if (mode == ClipDrag.Move && dragClip is not null) {
        dragClip.StartSample = Math.Max(0, Track.SnapToGrid(e.X * timescale - grabOffset));
        pan.Invalidate();
      }
      else if (mode == ClipDrag.Resize && dragClip is not null) {
        track.ResizeClip(dragClip, Track.SnapToGrid(e.X * timescale - dragClip.StartSample));
        pan.Invalidate();
      }
      else {
        // just hovering: show a resize cursor over a clip's right edge
        track.FindClipAt(e.X, timescale, LogicalToDeviceUnits(6), out bool overEdge);
        pan.Cursor = overEdge ? Cursors.SizeWE : Cursors.Default;
      }
    };

    pan.MouseUp += (sender, e) => {
      mode = ClipDrag.None;
      dragClip = null;
    };
  }
}
