using System;
using System.Collections.Generic;

namespace AnimationCreator.Data
{
    [Serializable]
    public class Vec3
    {
        public float x;
        public float y;
        public float z;

        public UnityEngine.Vector3 ToUnity() => new UnityEngine.Vector3(x, y, z);
    }

    [Serializable]
    public class MatchManifest
    {
        public string schemaVersion;
        public string matchId;
        public PitchInfo pitch;
        public TimingInfo timing;
        public SituationInfo situation;
        public DataSourceInfo dataSource;
        public TeamInfo[] teams;
        public PlayerInfo[] players;
    }

    [Serializable]
    public class SituationInfo
    {
        public string type;
        public string title;
        public string description;
        public int keyFrameIndex;
        public Vec3 kickPoint;
        public OffsideLineInfo offsideLine;
        public ReplayWindowInfo replayWindow;
        public VerdictInfo verdict;
    }

    [Serializable]
    public class VerdictInfo
    {
        public string outcome;
        public float marginM;
        public DecisionPointInfo attackerPoint;
        public DecisionPointInfo defenderPoint;
    }

    [Serializable]
    public class DecisionPointInfo
    {
        public string playerId;
        public string joint;
        public Vec3 position;
    }

    [Serializable]
    public class OffsideLineInfo
    {
        public float z;
        public string secondLastDefenderPlayerId;
        public string lastDefenderPlayerId;
        public string offsidePlayerId;
    }

    [Serializable]
    public class ReplayWindowInfo
    {
        public int startFrameIndex;
        public int endFrameIndex;
    }

    [Serializable]
    public class DataSourceInfo
    {
        public string type;
        public string clipId;
        public FrameRangeInfo frameRange;
        public string positions;
        public string joints;
        public string note;
        public string jointSynthesis;
        public string smoothing;
    }

    [Serializable]
    public class FrameRangeInfo
    {
        public int start;
        public int end;
    }

    [Serializable]
    public class PitchInfo
    {
        public float lengthM;
        public float widthM;
    }

    [Serializable]
    public class TimingInfo
    {
        public int frameRateHz;
        public float durationSeconds;
    }

    [Serializable]
    public class TeamInfo
    {
        public string teamId;
        public string name;
        public string color;
    }

    [Serializable]
    public class PlayerInfo
    {
        public string playerId;
        public string teamId;
        public int jerseyNumber;
        public string displayName;
    }

    [Serializable]
    public class MatchEventsFile
    {
        public MatchEvent[] events;
    }

    [Serializable]
    public class MatchEvent
    {
        public string eventId;
        public string type;
        public int frameIndex;
        public int timestampMs;
        public string actorPlayerId;
        public string targetPlayerId;
        public string foot;
    }

    [Serializable]
    public class MatchFrame
    {
        public int frameIndex;
        public int timestampMs;
        public BallFrame ball;
        public PlayerFrame[] players;
    }

    [Serializable]
    public class BallFrame
    {
        public Vec3 pos;
    }

    [Serializable]
    public class PlayerFrame
    {
        public string playerId;
        public PlayerJoints joints;
    }

    [Serializable]
    public class PlayerJoints
    {
        public Vec3 pelvis;
        public Vec3 torso;
        public Vec3 head;
        public Vec3 leftShoulder;
        public Vec3 rightShoulder;
        public Vec3 leftElbow;
        public Vec3 rightElbow;
        public Vec3 leftKnee;
        public Vec3 rightKnee;
        public Vec3 leftFoot;
        public Vec3 rightFoot;
    }

    public class MatchDataset
    {
        public MatchManifest Manifest;
        public List<MatchEvent> Events = new List<MatchEvent>();
        public List<MatchFrame> Frames = new List<MatchFrame>();

        public Dictionary<string, PlayerInfo> PlayersById = new Dictionary<string, PlayerInfo>();
        public Dictionary<string, string> TeamColors = new Dictionary<string, string>();
    }
}
