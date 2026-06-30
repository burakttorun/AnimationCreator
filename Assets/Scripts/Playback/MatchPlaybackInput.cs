using UnityEngine.InputSystem;

namespace AnimationCreator.Playback
{
  public static class MatchPlaybackInput
  {
    public static bool TogglePlayPressed => WasPressed(Key.Space);
    public static bool StepBackPressed => WasPressed(Key.LeftArrow);
    public static bool StepForwardPressed => WasPressed(Key.RightArrow);
    public static bool RestartPressed => WasPressed(Key.R);
    public static bool SeekKeyFramePressed => WasPressed(Key.K);
    public static bool CycleCameraPressed => WasPressed(Key.C);

    static bool WasPressed(Key key)
    {
      var keyboard = Keyboard.current;
      return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
  }
}
