using System.Globalization;
using System.Text.Json;

const int Fps = 25;
const float DurationSec = 10f;
const string SequenceId = "presentation_demo";

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var totalFrames = (int)(DurationSec * Fps);

var manifest = new
{
    schemaVersion = "0.2.0",
    matchId = SequenceId,
    pitch = new { lengthM = 105.0, widthM = 68.0 },
    timing = new { frameRateHz = Fps, durationSeconds = DurationSec },
    dataSource = new
    {
        positions = "procedural",
        joints = "procedural_gait",
        note = "Clean single-runner presentation clip with coherent stick-figure kinematics.",
    },
    teams = new[]
    {
        new { teamId = "home", name = "Home", color = "#E63946" },
    },
    players = new[]
    {
        new { playerId = "runner", teamId = "home", jerseyNumber = 10, displayName = "Runner" },
    },
};

var events = new { events = Array.Empty<object>() };
var frames = new List<string>();

for (var frame = 0; frame < totalFrames; frame++)
{
    var t = frame / (float)Math.Max(1, totalFrames - 1);
    var px = Mathf.Lerp(0f, 0f, t);
    var pz = Mathf.Lerp(-35f, 15f, SmoothStep(t));
    var face = 0f;
    var joints = MakeJoints(px, pz, face, frame);

    var line = new
    {
        frameIndex = frame,
        timestampMs = (int)Math.Round(frame * 1000.0 / Fps),
        ball = new { pos = Vec(px + 0.3f, 0.11f, pz + 0.2f) },
        players = new[] { new { playerId = "runner", joints } },
    };
    frames.Add(JsonSerializer.Serialize(line));
}

foreach (var dir in new[]
{
    Path.Combine(root, "SampleData", SequenceId),
    Path.Combine(root, "Assets", "StreamingAssets", SequenceId),
})
{
    Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOpts()));
    File.WriteAllText(Path.Combine(dir, "events.json"), JsonSerializer.Serialize(events, JsonOpts()));
    File.WriteAllText(Path.Combine(dir, "frames.jsonl"), string.Join('\n', frames) + '\n');
}

Console.WriteLine($"Wrote {SequenceId}: {frames.Count} frames @ {Fps} Hz");

static float SmoothStep(float t)
{
    t = Math.Clamp(t, 0f, 1f);
    return t * t * (3f - 2f * t);
}

static object MakeJoints(float px, float pz, float face, int frame)
{
    const float py = 0.92f;
    var phase = frame * 0.42f;
    var stride = 0.28f * MathF.Sin(phase);
    var armSwing = 0.22f * MathF.Sin(phase);
    var bounce = 0.025f * MathF.Abs(MathF.Sin(phase));

    var sinF = MathF.Sin(face);
    var cosF = MathF.Cos(face);
    var fwdX = sinF;
    var fwdZ = cosF;
    var rightX = cosF;
    var rightZ = -sinF;

    var pelvisY = py + bounce;
    var pelvisX = px;
    var pelvisZ = pz;

    var torsoX = pelvisX + fwdX * 0.04f;
    var torsoZ = pelvisZ + fwdZ * 0.04f;
    var headX = torsoX + fwdX * 0.07f;
    var headZ = torsoZ + fwdZ * 0.07f;

    var lsX = torsoX + rightX * (-0.22f);
    var lsZ = torsoZ + rightZ * (-0.22f);
    var rsX = torsoX + rightX * 0.22f;
    var rsZ = torsoZ + rightZ * 0.22f;

    var leX = lsX + fwdX * (-armSwing) + rightX * (-0.08f);
    var leZ = lsZ + fwdZ * (-armSwing) + rightZ * (-0.08f);
    var reX = rsX + fwdX * armSwing + rightX * 0.08f;
    var reZ = rsZ + fwdZ * armSwing + rightZ * 0.08f;

    var lkX = pelvisX + rightX * (-0.12f) + fwdX * stride * 0.25f;
    var lkZ = pelvisZ + rightZ * (-0.12f) + fwdZ * stride * 0.25f;
    var rkX = pelvisX + rightX * 0.12f + fwdX * (-stride) * 0.25f;
    var rkZ = pelvisZ + rightZ * 0.12f + fwdZ * (-stride) * 0.25f;

    var lfX = lkX + fwdX * stride;
    var lfZ = lkZ + fwdZ * stride;
    var rfX = rkX + fwdX * (-stride);
    var rfZ = rkZ + fwdZ * (-stride);

    return new
    {
        pelvis = Vec(pelvisX, pelvisY, pelvisZ),
        torso = Vec(torsoX, pelvisY + 0.48f, torsoZ),
        head = Vec(headX, pelvisY + 0.76f, headZ),
        leftShoulder = Vec(lsX, pelvisY + 0.50f, lsZ),
        rightShoulder = Vec(rsX, pelvisY + 0.50f, rsZ),
        leftElbow = Vec(leX, pelvisY + 0.36f, leZ),
        rightElbow = Vec(reX, pelvisY + 0.36f, reZ),
        leftKnee = Vec(lkX, 0.48f, lkZ),
        rightKnee = Vec(rkX, 0.48f, rkZ),
        leftFoot = Vec(lfX, 0.06f, lfZ),
        rightFoot = Vec(rfX, 0.06f, rfZ),
    };
}

static object Vec(float x, float y, float z) => new
{
    x = MathF.Round(x, 3),
    y = MathF.Round(y, 3),
    z = MathF.Round(z, 3),
};

static JsonSerializerOptions JsonOpts() => new() { WriteIndented = true };

static class Mathf
{
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Sin(float x) => MathF.Sin(x);
    public static float Cos(float x) => MathF.Cos(x);
    public static float Abs(float x) => MathF.Abs(x);
}
