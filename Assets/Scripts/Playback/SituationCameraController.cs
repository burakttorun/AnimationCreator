using UnityEngine;

namespace AnimationCreator.Playback
{
  public enum SituationCameraMode
  {
    Overview,
    SideLine,
    KickTight,
    VerdictCloseUp,
  }

  public class SituationCameraController : MonoBehaviour
  {
    [SerializeField] float followSpeed = 4f;

    SituationCameraMode _mode = SituationCameraMode.Overview;
    Vector3 _focus = Vector3.zero;
    float _lineZ;
    bool _verdictMode;

    public SituationCameraMode Mode => _mode;

    public void Configure(float offsideLineZ, Vector3 kickPoint)
    {
      _lineZ = offsideLineZ;
      _focus = new Vector3(kickPoint.x, 0f, (kickPoint.z + offsideLineZ) * 0.5f);
    }

    public void SetVerdictMode(bool active, Vector3 focusPos)
    {
      _verdictMode = active;
      if (active)
      {
        _focus = focusPos;
        _mode = SituationCameraMode.VerdictCloseUp;
      }
    }

    public void CycleMode()
    {
      if (_verdictMode)
        return;

      _mode = _mode switch
      {
        SituationCameraMode.Overview => SituationCameraMode.SideLine,
        SituationCameraMode.SideLine => SituationCameraMode.KickTight,
        SituationCameraMode.KickTight => SituationCameraMode.Overview,
        _ => SituationCameraMode.Overview,
      };
    }

    public void SetFocus(Vector3 worldPos)
    {
      if (!_verdictMode)
        _focus = worldPos;
    }

    void LateUpdate()
    {
      var (pos, look) = GetDesiredPose();
      var speed = _verdictMode ? followSpeed * 2.2f : followSpeed;
      transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * speed);
      transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - transform.position), Time.deltaTime * speed);
    }

    (Vector3 pos, Vector3 look) GetDesiredPose()
    {
      return _mode switch
      {
        SituationCameraMode.VerdictCloseUp => (
          _focus + new Vector3(-9f, 2.8f, -4.5f),
          _focus + new Vector3(0f, 1.1f, 0.3f)),
        SituationCameraMode.SideLine => (
          new Vector3(-42f, 8f, _lineZ),
          new Vector3(0f, 1f, _lineZ)),
        SituationCameraMode.KickTight => (
          new Vector3(_focus.x - 12f, 5f, _focus.z - 8f),
          new Vector3(_focus.x, 1.2f, _lineZ)),
        _ => (
          new Vector3(0f, 38f, _lineZ - 28f),
          new Vector3(0f, 0f, _lineZ + 6f)),
      };
    }
  }
}
