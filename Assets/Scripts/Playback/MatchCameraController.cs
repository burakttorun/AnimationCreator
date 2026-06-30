using UnityEngine;

namespace AnimationCreator.Playback
{
  public enum MatchCameraMode
  {
    Minimap,
    Broadcast,
    SideLine,
  }

  public class MatchCameraController : MonoBehaviour
  {
    [SerializeField] float followSpeed = 5f;
    [SerializeField] float modeBlendSpeed = 6f;

    MatchCameraMode _mode = MatchCameraMode.Minimap;
    Transform _ball;
    Vector3 _worldOffset;
    Quaternion _targetRotation;
    bool _blendingMode;

    public MatchCameraMode Mode => _mode;

    void Awake()
    {
      ApplyMode(_mode, immediate: true);
    }

    public void CycleMode()
    {
      _mode = _mode switch
      {
        MatchCameraMode.Minimap => MatchCameraMode.Broadcast,
        MatchCameraMode.Broadcast => MatchCameraMode.SideLine,
        _ => MatchCameraMode.Minimap,
      };

      ApplyMode(_mode, immediate: false);
    }

    void LateUpdate()
    {
      if (_ball == null)
      {
        var ballGo = GameObject.Find("Ball");
        if (ballGo != null)
          _ball = ballGo.transform;
      }

      var ballPos = _ball != null ? _ball.position : Vector3.zero;
      var desiredPos = new Vector3(ballPos.x + _worldOffset.x, _worldOffset.y, ballPos.z + _worldOffset.z);
      transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * followSpeed);

      if (_blendingMode)
      {
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * modeBlendSpeed);
        if (Quaternion.Angle(transform.rotation, _targetRotation) < 0.5f)
        {
          transform.rotation = _targetRotation;
          _blendingMode = false;
        }
      }
    }

    void ApplyMode(MatchCameraMode mode, bool immediate)
    {
      var preset = GetPreset(mode);
      _worldOffset = preset.offset;
      _targetRotation = preset.rotation;

      if (immediate)
      {
        transform.rotation = _targetRotation;
        _blendingMode = false;

        if (_ball != null)
        {
          var ballPos = _ball.position;
          transform.position = new Vector3(ballPos.x + _worldOffset.x, _worldOffset.y, ballPos.z + _worldOffset.z);
        }
      }
      else
      {
        _blendingMode = true;
      }
    }

    static (Vector3 offset, Quaternion rotation) GetPreset(MatchCameraMode mode)
    {
      return mode switch
      {
        MatchCameraMode.Broadcast => (
          new Vector3(0f, 36f, -26f),
          Quaternion.Euler(52f, 0f, 0f)),
        MatchCameraMode.SideLine => (
          new Vector3(-38f, 10f, 0f),
          Quaternion.Euler(12f, 90f, 0f)),
        _ => (
          new Vector3(0f, 52f, 0f),
          Quaternion.Euler(90f, 0f, 0f)),
      };
    }
  }
}
