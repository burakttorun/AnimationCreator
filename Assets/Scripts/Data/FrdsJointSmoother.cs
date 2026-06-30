using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationCreator.Data
{
  /// <summary>Temporal smoothing for FRDS joint tracks (load-time or runtime).</summary>
  public static class FrdsJointSmoother
  {
    public struct Settings
    {
      public float WindowSeconds;
      public float MaxJointSpeedMps;
      public bool Enabled;

      public static Settings Default => new Settings
      {
        Enabled = true,
        WindowSeconds = 0.18f,
        MaxJointSpeedMps = 8f
      };
    }

    public static void SmoothDataset(MatchDataset dataset, Settings settings)
    {
      if (!settings.Enabled || dataset.Frames.Count < 3)
        return;

      var fps = Mathf.Max(1, dataset.Manifest.timing.frameRateHz);
      var radius = Mathf.Max(1, Mathf.RoundToInt(settings.WindowSeconds * fps));

      var playerIds = new List<string>();
      foreach (var p in dataset.Manifest.players)
        playerIds.Add(p.playerId);

      foreach (var playerId in playerIds)
        SmoothPlayerTrack(dataset, playerId, radius, settings.MaxJointSpeedMps, fps);
    }

    static void SmoothPlayerTrack(MatchDataset dataset, string playerId, int radius, float maxSpeed, int fps)
    {
      var count = dataset.Frames.Count;
      var pelvis = new Vector3[count];
      var torso = new Vector3[count];
      var head = new Vector3[count];
      var lShoulder = new Vector3[count];
      var rShoulder = new Vector3[count];
      var lElbow = new Vector3[count];
      var rElbow = new Vector3[count];
      var lKnee = new Vector3[count];
      var rKnee = new Vector3[count];
      var lFoot = new Vector3[count];
      var rFoot = new Vector3[count];
      var hasArms = new bool[count];

      for (var i = 0; i < count; i++)
      {
        var joints = FindJoints(dataset.Frames[i], playerId);
        if (joints == null)
          continue;

        pelvis[i] = joints.pelvis.ToUnity();
        torso[i] = joints.torso.ToUnity();
        head[i] = joints.head.ToUnity();
        lKnee[i] = joints.leftKnee.ToUnity();
        rKnee[i] = joints.rightKnee.ToUnity();
        lFoot[i] = joints.leftFoot.ToUnity();
        rFoot[i] = joints.rightFoot.ToUnity();
        hasArms[i] = PlayerJointsUtility.HasArmData(joints);
        if (hasArms[i])
        {
          lShoulder[i] = joints.leftShoulder.ToUnity();
          rShoulder[i] = joints.rightShoulder.ToUnity();
          lElbow[i] = joints.leftElbow.ToUnity();
          rElbow[i] = joints.rightElbow.ToUnity();
        }
      }

      pelvis = MovingAverage(pelvis, radius);
      torso = MovingAverage(torso, radius);
      head = MovingAverage(head, radius);
      lKnee = MovingAverage(lKnee, radius);
      rKnee = MovingAverage(rKnee, radius);
      lFoot = MovingAverage(lFoot, radius);
      rFoot = MovingAverage(rFoot, radius);
      lShoulder = MovingAverage(lShoulder, radius);
      rShoulder = MovingAverage(rShoulder, radius);
      lElbow = MovingAverage(lElbow, radius);
      rElbow = MovingAverage(rElbow, radius);

      var maxDelta = maxSpeed / fps;
      pelvis = ClampVelocity(pelvis, maxDelta);
      torso = ClampVelocity(torso, maxDelta);
      head = ClampVelocity(head, maxDelta);
      lKnee = ClampVelocity(lKnee, maxDelta);
      rKnee = ClampVelocity(rKnee, maxDelta);
      lFoot = ClampVelocityHorizontal(lFoot, maxDelta);
      rFoot = ClampVelocityHorizontal(rFoot, maxDelta);
      lShoulder = ClampVelocity(lShoulder, maxDelta);
      rShoulder = ClampVelocity(rShoulder, maxDelta);
      lElbow = ClampVelocity(lElbow, maxDelta);
      rElbow = ClampVelocity(rElbow, maxDelta);

      for (var i = 0; i < count; i++)
      {
        var pf = FindPlayerFrame(dataset.Frames[i], playerId);
        if (pf == null)
          continue;

        pf.joints.pelvis = ToVec(pelvis[i]);
        pf.joints.torso = ToVec(torso[i]);
        pf.joints.head = ToVec(head[i]);
        pf.joints.leftKnee = ToVec(lKnee[i]);
        pf.joints.rightKnee = ToVec(rKnee[i]);
        pf.joints.leftFoot = ToVec(lFoot[i]);
        pf.joints.rightFoot = ToVec(rFoot[i]);

        PlayerJointsGrounding.Sanitize(pf.joints);

        if (hasArms[i])
        {
          pf.joints.leftShoulder = ToVec(lShoulder[i]);
          pf.joints.rightShoulder = ToVec(rShoulder[i]);
          pf.joints.leftElbow = ToVec(lElbow[i]);
          pf.joints.rightElbow = ToVec(rElbow[i]);
        }
      }
    }

    static PlayerJoints FindJoints(MatchFrame frame, string playerId)
    {
      foreach (var pf in frame.players)
      {
        if (pf.playerId == playerId)
          return pf.joints;
      }

      return null;
    }

    static PlayerFrame FindPlayerFrame(MatchFrame frame, string playerId)
    {
      foreach (var pf in frame.players)
      {
        if (pf.playerId == playerId)
          return pf;
      }

      return null;
    }

    static Vector3[] MovingAverage(Vector3[] src, int radius)
    {
      var dst = new Vector3[src.Length];
      for (var i = 0; i < src.Length; i++)
      {
        var sum = Vector3.zero;
        var n = 0;
        var from = Mathf.Max(0, i - radius);
        var to = Mathf.Min(src.Length - 1, i + radius);
        for (var j = from; j <= to; j++)
        {
          sum += src[j];
          n++;
        }

        dst[i] = n > 0 ? sum / n : src[i];
      }

      return dst;
    }

    static Vector3[] ClampVelocityHorizontal(Vector3[] src, float maxDelta)
    {
      if (src.Length == 0)
        return src;

      var dst = new Vector3[src.Length];
      dst[0] = src[0];
      for (var i = 1; i < src.Length; i++)
      {
        var prev = dst[i - 1];
        var target = src[i];
        var flat = new Vector3(target.x, prev.y, target.z);
        flat = Vector3.MoveTowards(new Vector3(prev.x, prev.y, prev.z), flat, maxDelta);
        dst[i] = new Vector3(flat.x, target.y, flat.z);
      }

      return dst;
    }

    static Vector3[] ClampVelocity(Vector3[] src, float maxDelta)
    {
      if (src.Length == 0)
        return src;

      var dst = new Vector3[src.Length];
      dst[0] = src[0];
      for (var i = 1; i < src.Length; i++)
        dst[i] = Vector3.MoveTowards(dst[i - 1], src[i], maxDelta);
      return dst;
    }

    static Vec3 ToVec(Vector3 v) => new Vec3 { x = v.x, y = v.y, z = v.z };
  }
}
