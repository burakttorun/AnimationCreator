using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace BvhToFrds;

static class Program
{
    const int TargetFps = 25;
    const float TargetPelvisHeightM = 0.92f;

    static readonly Dictionary<string, string> JointMap = new(StringComparer.Ordinal)
    {
        ["Hips"] = "pelvis",
        ["Spine1"] = "torso",
        ["Head"] = "head",
        ["LeftShoulder"] = "leftShoulder",
        ["RightShoulder"] = "rightShoulder",
        ["LeftArm"] = "leftElbow",
        ["RightArm"] = "rightElbow",
        ["LeftLeg"] = "leftKnee",
        ["RightLeg"] = "rightKnee",
        ["LeftFoot"] = "leftFoot",
        ["RightFoot"] = "rightFoot",
    };

    static readonly string[] RequiredJoints =
    [
        "pelvis", "torso", "head",
        "leftShoulder", "rightShoulder", "leftElbow", "rightElbow",
        "leftKnee", "rightKnee", "leftFoot", "rightFoot",
    ];

    public static int Main(string[] args)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var bvhPath = args.Length > 0
            ? args[0]
            : Path.Combine(root, "Tools", "sample_data", "09_01", "09_01.bvh");
        var sequenceId = args.Length > 1 ? args[1] : "mocap_run_demo";
        var loopCount = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 3;

        if (!File.Exists(bvhPath))
        {
            Console.Error.WriteLine($"BVH not found: {bvhPath}");
            return 1;
        }

        var motion = BvhParser.Load(bvhPath);
        var sourceFps = 1.0 / motion.FrameTimeSeconds;
        var solved = new List<Dictionary<string, Vector3>>();

        foreach (var frame in motion.Frames)
            solved.Add(MapToFrds(BvhParser.SolveFrame(motion, frame)));

        NormalizeSkeleton(solved);
        AlignToPitch(solved);

        var resampled = Resample(solved, sourceFps, TargetFps);
        var looped = LoopFrames(resampled, loopCount);
        var durationSec = looped.Count / (float)TargetFps;

        var manifest = new
        {
            schemaVersion = "0.2.0",
            matchId = sequenceId,
            pitch = new { lengthM = 105.0, widthM = 68.0 },
            timing = new { frameRateHz = TargetFps, durationSeconds = durationSec },
            dataSource = new
            {
                positions = "synthetic",
                joints = "cmu_mocap_bvh",
                note = $"CMU subject 09 run clip 09_01, converted from BVH ({Path.GetFileName(bvhPath)}).",
            },
            teams = new[]
            {
                new { teamId = "home", name = "Home", color = "#E63946" },
                new { teamId = "away", name = "Away", color = "#457B9D" },
            },
            players = new[]
            {
                new
                {
                    playerId = "runner",
                    teamId = "home",
                    jerseyNumber = 10,
                    displayName = "Runner",
                },
            },
        };

        var events = new { events = Array.Empty<object>() };
        var outputDirs = new[]
        {
            Path.Combine(root, "SampleData", sequenceId),
            Path.Combine(root, "Assets", "StreamingAssets", sequenceId),
        };

        foreach (var dir in outputDirs)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions()));
            File.WriteAllText(Path.Combine(dir, "events.json"), JsonSerializer.Serialize(events, JsonOptions()));

            using var writer = new StreamWriter(Path.Combine(dir, "frames.jsonl"));
            for (var i = 0; i < looped.Count; i++)
            {
                var pelvis = looped[i]["pelvis"];
                var line = new
                {
                    frameIndex = i,
                    timestampMs = (int)Math.Round(i * 1000.0 / TargetFps),
                    ball = new { pos = Vec(pelvis.X + 0.35f, 0.11f, pelvis.Z + 0.25f) },
                    players = new[]
                    {
                        new { playerId = "runner", joints = ToJointDict(looped[i]) },
                    },
                };
                writer.WriteLine(JsonSerializer.Serialize(line));
            }
        }

        Console.WriteLine($"Wrote {sequenceId}: {looped.Count} frames @ {TargetFps} Hz ({durationSec:F1}s)");
        Console.WriteLine($"Source: {bvhPath}");
        return 0;
    }

    static Dictionary<string, Vector3> MapToFrds(Dictionary<string, Vector3> bvh)
    {
        const float bvhUnitToMeters = 0.01f;
        var mapped = new Dictionary<string, Vector3>();
        foreach (var pair in JointMap)
        {
            if (bvh.TryGetValue(pair.Key, out var pos))
                mapped[pair.Value] = pos * bvhUnitToMeters;
        }

        if (!mapped.ContainsKey("torso") && bvh.TryGetValue("Spine", out var spine))
            mapped["torso"] = spine * bvhUnitToMeters;

        return mapped;
    }

    static void NormalizeSkeleton(List<Dictionary<string, Vector3>> frames)
    {
        var first = frames[0];
        var footY = MathF.Min(first["leftFoot"].Y, first["rightFoot"].Y);
        var pelvisY = first["pelvis"].Y;
        var height = pelvisY - footY;
        if (height < 1e-4f)
            throw new InvalidDataException("Invalid skeleton height.");

        var scale = TargetPelvisHeightM / height;
        var origin = new Vector3(first["pelvis"].X, footY, first["pelvis"].Z);

        foreach (var frame in frames)
        {
            var keys = frame.Keys.ToList();
            foreach (var key in keys)
                frame[key] = (frame[key] - origin) * scale;
        }
    }

    static void AlignToPitch(List<Dictionary<string, Vector3>> frames)
    {
        var first = frames[0];
        var last = frames[^1];
        var delta = last["pelvis"] - first["pelvis"];
        delta.Y = 0f;
        if (delta.LengthSquared() < 1e-6f)
            delta = new Vector3(0f, 0f, 1f);

        var angle = MathF.Atan2(delta.X, delta.Z);
        var rotation = Matrix4x4.CreateRotationY(-angle);

        foreach (var frame in frames)
        {
            var keys = frame.Keys.ToList();
            foreach (var key in keys)
                frame[key] = Vector3.Transform(frame[key], rotation);
        }

        var start = new Vector3(0f, 0f, -25f);
        var strideBoost = 3.5f;

        for (var i = 0; i < frames.Count; i++)
        {
            var t = i / (float)Math.Max(1, frames.Count - 1);
            var travel = new Vector3(0f, 0f, t * strideBoost);
            var keys = frames[i].Keys.ToList();
            foreach (var key in keys)
                frames[i][key] = SwapYUpToFrds(frames[i][key] + start + travel);
        }
    }

    static Vector3 SwapYUpToFrds(Vector3 v) => new(v.X, v.Y, v.Z);

    static List<Dictionary<string, Vector3>> Resample(
        List<Dictionary<string, Vector3>> source,
        double sourceFps,
        int targetFps)
    {
        if (source.Count == 0)
            return source;

        var duration = (source.Count - 1) / sourceFps;
        var targetCount = Math.Max(1, (int)Math.Round(duration * targetFps) + 1);
        var result = new List<Dictionary<string, Vector3>>(targetCount);

        for (var i = 0; i < targetCount; i++)
        {
            var t = i / (double)Math.Max(1, targetCount - 1) * (source.Count - 1);
            var i0 = (int)Math.Floor(t);
            var i1 = Math.Min(source.Count - 1, i0 + 1);
            var alpha = (float)(t - i0);
            result.Add(LerpFrames(source[i0], source[i1], alpha));
        }

        return result;
    }

    static Dictionary<string, Vector3> LerpFrames(
        Dictionary<string, Vector3> a,
        Dictionary<string, Vector3> b,
        float t)
    {
        var frame = new Dictionary<string, Vector3>();
        foreach (var joint in RequiredJoints)
            frame[joint] = Vector3.Lerp(a[joint], b[joint], t);
        return frame;
    }

    static List<Dictionary<string, Vector3>> LoopFrames(
        List<Dictionary<string, Vector3>> source,
        int loopCount)
    {
        if (loopCount <= 1)
            return source;

        var result = new List<Dictionary<string, Vector3>>(source.Count * loopCount);
        var stride = source[^1]["pelvis"].Z - source[0]["pelvis"].Z;

        for (var loop = 0; loop < loopCount; loop++)
        {
            var offset = new Vector3(0f, 0f, stride * loop);
            foreach (var frame in source)
            {
                var copy = new Dictionary<string, Vector3>();
                foreach (var pair in frame)
                    copy[pair.Key] = pair.Value + offset;
                result.Add(copy);
            }
        }

        return result;
    }

    static object ToJointDict(Dictionary<string, Vector3> joints)
    {
        var dict = new Dictionary<string, object>();
        foreach (var joint in RequiredJoints)
            dict[joint] = Vec(joints[joint]);
        return dict;
    }

    static object Vec(Vector3 v) => new
    {
        x = MathF.Round(v.X, 3),
        y = MathF.Round(v.Y, 3),
        z = MathF.Round(v.Z, 3),
    };

    static object Vec(float x, float y, float z) => new
    {
        x = MathF.Round(x, 3),
        y = MathF.Round(y, 3),
        z = MathF.Round(z, 3),
    };

    static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
}
