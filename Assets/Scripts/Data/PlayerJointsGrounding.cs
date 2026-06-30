using UnityEngine;

namespace AnimationCreator.Data
{
  /// <summary>Ground contact helpers for FRDS joint data and humanoid replay.</summary>
  public static class PlayerJointsGrounding
  {
    public const float DefaultGroundY = 0f;
    public const float DefaultSoleClearance = 0.04f;

    /// <summary>FRDS foot joint = sole contact; clamp and lift swing feet.</summary>
    public static PlayerJoints Sanitize(PlayerJoints joints, float groundY = DefaultGroundY)
    {
      var minFootY = groundY + DefaultSoleClearance;
      joints.leftFoot = ClampFoot(joints.leftFoot, minFootY);
      joints.rightFoot = ClampFoot(joints.rightFoot, minFootY);
      return joints;
    }

    /// <summary>Offset foot targets so humanoid foot bone (ankle) matches sole contact in data.</summary>
    public static Vector3 FootBoneTarget(Vector3 soleContact, float ankleAboveSole)
    {
      return soleContact + Vector3.up * ankleAboveSole;
    }

    static Vec3 ClampFoot(Vec3 foot, float minY)
    {
      if (foot == null)
        return foot;
      if (foot.y < minY)
        foot.y = minY;
      return foot;
    }
  }
}
