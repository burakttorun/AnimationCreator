using UnityEngine;

namespace AnimationCreator.Data
{
  public static class PlayerJointsUtility
  {
    public static bool HasArmData(PlayerJoints joints) =>
      joints.leftShoulder != null && joints.leftElbow != null &&
      joints.rightShoulder != null && joints.rightElbow != null;

    public static PlayerJoints Lerp(PlayerJoints a, PlayerJoints b, float t)
    {
      return new PlayerJoints
      {
        pelvis = LerpVec(a.pelvis, b.pelvis, t),
        torso = LerpVec(a.torso, b.torso, t),
        head = LerpVec(a.head, b.head, t),
        leftShoulder = LerpVec(a.leftShoulder, b.leftShoulder, t),
        rightShoulder = LerpVec(a.rightShoulder, b.rightShoulder, t),
        leftElbow = LerpVec(a.leftElbow, b.leftElbow, t),
        rightElbow = LerpVec(a.rightElbow, b.rightElbow, t),
        leftKnee = LerpVec(a.leftKnee, b.leftKnee, t),
        rightKnee = LerpVec(a.rightKnee, b.rightKnee, t),
        leftFoot = LerpVec(a.leftFoot, b.leftFoot, t),
        rightFoot = LerpVec(a.rightFoot, b.rightFoot, t)
      };
    }

    static Vec3 LerpVec(Vec3 a, Vec3 b, float t)
    {
      if (a == null && b == null)
        return null;
      if (a == null)
        return b;
      if (b == null)
        return a;

      return new Vec3
      {
        x = Mathf.Lerp(a.x, b.x, t),
        y = Mathf.Lerp(a.y, b.y, t),
        z = Mathf.Lerp(a.z, b.z, t)
      };
    }
  }
}
