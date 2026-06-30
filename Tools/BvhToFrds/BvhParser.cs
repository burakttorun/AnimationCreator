using System.Globalization;
using System.Numerics;

namespace BvhToFrds;

sealed class BvhJoint
{
    public required string Name;
    public Vector3 Offset;
    public bool IsEndSite;
    public List<string> Channels = new();
    public List<BvhJoint> Children = new();
    public BvhJoint? Parent;
}

sealed class BvhMotion
{
    public required BvhJoint Root;
    public int FrameCount;
    public double FrameTimeSeconds;
    public required List<float[]> Frames;
    public Dictionary<string, BvhJoint> JointsByName = new();
}

static class BvhParser
{
    public static BvhMotion Load(string path)
    {
        var lines = File.ReadAllLines(path);
        var index = 0;
        while (index < lines.Length && !lines[index].TrimStart().StartsWith("ROOT", StringComparison.Ordinal))
            index++;

        if (index >= lines.Length)
            throw new InvalidDataException("BVH file missing ROOT joint.");

        var root = ParseJoint(lines, ref index, null);
        while (index < lines.Length && !lines[index].TrimStart().StartsWith("MOTION", StringComparison.Ordinal))
            index++;

        if (index >= lines.Length)
            throw new InvalidDataException("BVH file missing MOTION section.");

        index++;
        var frameCount = int.Parse(lines[index].Split(':')[1].Trim(), CultureInfo.InvariantCulture);
        index++;
        var frameTime = double.Parse(lines[index].Split(':')[1].Trim(), CultureInfo.InvariantCulture);
        index++;

        var frames = new List<float[]>();
        for (var f = 0; f < frameCount && index < lines.Length; f++, index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                f--;
                continue;
            }

            frames.Add(lines[index]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => float.Parse(v, CultureInfo.InvariantCulture))
                .ToArray());
        }

        var motion = new BvhMotion
        {
            Root = root,
            FrameCount = frameCount,
            FrameTimeSeconds = frameTime,
            Frames = frames,
        };
        IndexJoints(root, motion.JointsByName);
        return motion;
    }

    static void IndexJoints(BvhJoint joint, Dictionary<string, BvhJoint> map)
    {
        if (!joint.IsEndSite)
            map[joint.Name] = joint;
        foreach (var child in joint.Children)
            IndexJoints(child, map);
    }

    static BvhJoint ParseJoint(string[] lines, ref int index, BvhJoint? parent)
    {
        var header = lines[index].Trim();
        index++;

        var joint = new BvhJoint
        {
            Name = header switch
            {
                var h when h.StartsWith("ROOT", StringComparison.Ordinal) => h.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[1],
                var h when h.StartsWith("JOINT", StringComparison.Ordinal) => h.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[1],
                _ => "End Site",
            },
            Parent = parent,
            IsEndSite = header.StartsWith("End Site", StringComparison.Ordinal),
        };

        if (index >= lines.Length || lines[index].Trim() != "{")
            throw new InvalidDataException($"Expected '{{' after joint {joint.Name}.");
        index++;

        while (index < lines.Length)
        {
            var line = lines[index].Trim();
            if (line == "}")
            {
                index++;
                break;
            }

            if (line.StartsWith("OFFSET", StringComparison.Ordinal))
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                joint.Offset = new Vector3(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture));
                index++;
                continue;
            }

            if (line.StartsWith("CHANNELS", StringComparison.Ordinal))
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 2; i < parts.Length; i++)
                    joint.Channels.Add(parts[i]);
                index++;
                continue;
            }

            if (line.StartsWith("JOINT", StringComparison.Ordinal) || line.StartsWith("End Site", StringComparison.Ordinal))
            {
                var child = ParseJoint(lines, ref index, joint);
                joint.Children.Add(child);
                continue;
            }

            throw new InvalidDataException($"Unexpected line in joint {joint.Name}: {line}");
        }

        return joint;
    }

    public static Dictionary<string, Vector3> SolveFrame(BvhMotion motion, float[] values)
    {
        var cursor = 0;
        var world = new Dictionary<string, Vector3>();
        SolveJoint(motion.Root, Matrix4x4.Identity, values, ref cursor, world);
        return world;
    }

    static void SolveJoint(BvhJoint joint, Matrix4x4 parentWorld, float[] values, ref int cursor, Dictionary<string, Vector3> world)
    {
        if (joint.IsEndSite)
            return;

        var local = Matrix4x4.Identity;
        Vector3 position = Vector3.Zero;
        var hasPosition = false;

        foreach (var channel in joint.Channels)
        {
            var value = values[cursor++];
            switch (channel)
            {
                case "Xposition":
                    position.X = value;
                    hasPosition = true;
                    break;
                case "Yposition":
                    position.Y = value;
                    hasPosition = true;
                    break;
                case "Zposition":
                    position.Z = value;
                    hasPosition = true;
                    break;
                case "Xrotation":
                    local *= Matrix4x4.CreateRotationX(value * MathF.PI / 180f);
                    break;
                case "Yrotation":
                    local *= Matrix4x4.CreateRotationY(value * MathF.PI / 180f);
                    break;
                case "Zrotation":
                    local *= Matrix4x4.CreateRotationZ(value * MathF.PI / 180f);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported channel: {channel}");
            }
        }

        if (hasPosition)
            local = Matrix4x4.CreateTranslation(position) * local;

        local *= Matrix4x4.CreateTranslation(joint.Offset);

        var worldMatrix = parentWorld * local;
        var worldPos = Vector3.Transform(Vector3.Zero, worldMatrix);
        world[joint.Name] = worldPos;

        foreach (var child in joint.Children)
            SolveJoint(child, worldMatrix, values, ref cursor, world);
    }
}
