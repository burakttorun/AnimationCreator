import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { pitchToFrds, GROUND_Y } from "./lib/pose_from_root.mjs";
import { buildJointsFromRoot } from "./lib/pose_from_root.mjs";
import { buildTrackingMarkerJoints } from "./lib/tracking_marker.mjs";
import { buildSmoothedRootTracks, buildReplayRootTracks, smoothBallTrack } from "./lib/root_track.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const FPS = 25;
const BALL_R = 0.11;
const PITCH = { lengthM: 105, widthM: 68 };

function parseArgs(argv) {
  const args = {
    input: "",
    sequenceId: "match_clip_demo",
    startFrame: 375,
    durationSec: 8,
    maxPlayers: 22,
    focusRadiusM: 40,
    jointMode: "tracking",
    sourceType: "soccernet_gs_labels",
    sourceNote: "SNGS-116 frames 375–574 — open play, clean on-pitch ball track",
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--input") args.input = argv[++i];
    else if (a === "--sequence-id") args.sequenceId = argv[++i];
    else if (a === "--start-frame") args.startFrame = +argv[++i];
    else if (a === "--duration-sec") args.durationSec = +argv[++i];
    else if (a === "--max-players") args.maxPlayers = +argv[++i];
    else if (a === "--focus-radius") args.focusRadiusM = +argv[++i];
    else if (a === "--joint-mode") args.jointMode = argv[++i];
    else if (a === "--source-type") args.sourceType = argv[++i];
    else if (a === "--source-note") args.sourceNote = argv[++i];
  }
  if (!args.input) throw new Error("--input required");
  return args;
}

function loadGsJson(filePath) {
  const raw = fs.readFileSync(filePath, "utf8").replace(/\bNaN\b/g, "null");
  const data = JSON.parse(raw);
  const list = data.predictions ?? data.labels ?? data.annotations ?? [];
  if (!Array.isArray(list)) throw new Error("No predictions/labels array in input JSON");
  return list;
}

function frameFromImageId(imageId) {
  return parseInt(String(imageId).slice(-4), 10) - 1;
}

function getPitchPos(det) {
  const bp = det.bbox_pitch;
  if (!bp) return null;
  if (typeof bp.x_bottom_middle === "number") return { x: bp.x_bottom_middle, y: bp.y_bottom_middle };
  if (Array.isArray(bp) && bp.length >= 2) return { x: bp[0], y: bp[1] };
  return null;
}

function teamIdFromSide(team) {
  return team === "right" ? "away" : "home";
}

function playerIdFromTrack(teamId, trackId) {
  const prefix = teamId === "home" ? "h" : "a";
  return `${prefix}t${trackId}`;
}

function fillTrackGaps(samples, totalFrames) {
  const out = samples.map((s) => (s ? { ...s } : null));
  let i = 0;
  while (i < totalFrames) {
    if (out[i]) {
      i++;
      continue;
    }
    let j = i;
    while (j < totalFrames && !out[j]) j++;
    const left = i > 0 ? out[i - 1] : null;
    const right = j < totalFrames ? out[j] : null;
    if (left && right) {
      const span = j - i;
      for (let k = 0; k < span; k++) {
        const t = (k + 1) / (span + 1);
        out[i + k] = {
          x: left.x + (right.x - left.x) * t,
          z: left.z + (right.z - left.z) * t,
        };
      }
    } else if (left) {
      for (let k = i; k < j; k++) out[k] = { ...left };
    } else if (right) {
      for (let k = i; k < j; k++) out[k] = { ...right };
    }
    i = j;
  }
  return out;
}

function pickTopTracks(trackStats, maxPlayers, focusX, focusZ, focusRadius) {
  return [...trackStats.entries()]
    .filter(([, s]) => {
      const dx = s.avgX - focusX;
      const dz = s.avgZ - focusZ;
      return Math.hypot(dx, dz) <= focusRadius && s.count >= s.minFrames * 0.5;
    })
    .sort((a, b) => b[1].score - a[1].score)
    .slice(0, maxPlayers)
    .map(([trackId]) => trackId);
}

function clipCentroid(trackStats) {
  let sumX = 0;
  let sumZ = 0;
  let n = 0;
  for (const s of trackStats.values()) {
    sumX += s.avgX;
    sumZ += s.avgZ;
    n++;
  }
  return { x: sumX / Math.max(1, n), z: sumZ / Math.max(1, n) };
}

function extractBallTrack(detections, startFrame, endFrame, totalFrames) {
  const PITCH_HW = PITCH.widthM * 0.5;
  const PITCH_HL = PITCH.lengthM * 0.5;
  const MAX_STEP_M = 2.8;

  const trackCounts = new Map();
  for (const det of detections) {
    const role = det.attributes?.role ?? det.role;
    if (role !== "ball") continue;
    const fi = frameFromImageId(det.image_id);
    if (fi < startFrame || fi > endFrame) continue;
    trackCounts.set(det.track_id, (trackCounts.get(det.track_id) ?? 0) + 1);
  }

  let mainTrack = null;
  let mainCount = 0;
  for (const [trackId, count] of trackCounts) {
    if (count > mainCount) {
      mainTrack = trackId;
      mainCount = count;
    }
  }

  const raw = new Array(totalFrames).fill(null);
  for (const det of detections) {
    const role = det.attributes?.role ?? det.role;
    if (role !== "ball") continue;
    if (mainTrack != null && det.track_id !== mainTrack) continue;
    const fi = frameFromImageId(det.image_id);
    if (fi < startFrame || fi > endFrame) continue;
    const pos = getPitchPos(det);
    if (!pos) continue;
    const frds = pitchToFrds(pos.x, pos.y);
    raw[fi - startFrame] = { x: frds.x, z: frds.z };
  }

  const cleaned = new Array(totalFrames).fill(null);
  let lastGood = null;
  for (let i = 0; i < totalFrames; i++) {
    const p = raw[i];
    let ok =
      p &&
      Number.isFinite(p.x) &&
      Number.isFinite(p.z) &&
      Math.abs(p.x) <= PITCH_HW + 1.2 &&
      Math.abs(p.z) <= PITCH_HL + 1.2;
    if (ok && lastGood) {
      const step = Math.hypot(p.x - lastGood.x, p.z - lastGood.z);
      if (step > MAX_STEP_M) ok = false;
    }
    if (ok) {
      cleaned[i] = { x: p.x, z: p.z };
      lastGood = p;
    } else if (lastGood) {
      cleaned[i] = { x: lastGood.x, z: lastGood.z };
    }
  }

  let first = cleaned.findIndex(Boolean);
  if (first > 0) {
    const seed = cleaned[first];
    for (let i = 0; i < first; i++) cleaned[i] = { ...seed };
  }

  const filled = fillTrackGaps(
    cleaned.map((p) => (p ? { x: p.x, z: p.z } : null)),
    totalFrames,
  );

  const positions = filled.map((p) =>
    p ? { x: +p.x.toFixed(3), y: BALL_R, z: +p.z.toFixed(3) } : { x: 0, y: BALL_R, z: 0 },
  );

  return smoothBallTrack(positions, FPS, { window: 2, maxStep: 1.2 / FPS });
}

function main() {
  const args = parseArgs(process.argv);
  const inputPath = path.isAbsolute(args.input) ? args.input : path.join(ROOT, args.input);
  const detections = loadGsJson(inputPath);
  const endFrame = args.startFrame + Math.floor(args.durationSec * FPS) - 1;
  const totalFrames = endFrame - args.startFrame + 1;
  const trackingMode = args.jointMode === "tracking";
  const buildJoints = trackingMode ? buildTrackingMarkerJoints : buildJointsFromRoot;

  const byFrame = new Map();
  for (const det of detections) {
    const role = det.attributes?.role ?? det.role ?? "player";
    if (role !== "player" && role !== "goalkeeper") continue;
    const fi = frameFromImageId(det.image_id);
    if (fi < args.startFrame || fi > endFrame) continue;
    const pos = getPitchPos(det);
    if (!pos) continue;

    const trackId = det.track_id;
    if (!byFrame.has(fi)) byFrame.set(fi, new Map());
    byFrame.get(fi).set(trackId, {
      trackId,
      role,
      team: det.attributes?.team ?? det.team_side ?? det.team ?? "left",
      jersey: det.attributes?.jersey ?? det.jersey_number ?? null,
      x: pos.x,
      y: pos.y,
    });
  }

  const frameIndices = [...byFrame.keys()].sort((a, b) => a - b);
  if (frameIndices.length < 10) throw new Error(`Too few frames in range (${frameIndices.length})`);

  const playerMeta = new Map();
  const trackStats = new Map();
  const rawByTrack = new Map();

  for (let local = 0; local < totalFrames; local++) {
    const fi = args.startFrame + local;
    const frameTracks = byFrame.get(fi);
    if (!frameTracks) continue;

    for (const e of frameTracks.values()) {
      const teamId = teamIdFromSide(e.team);
      const jersey = e.jersey != null ? +e.jersey : e.trackId;
      if (!playerMeta.has(e.trackId)) {
        playerMeta.set(e.trackId, {
          playerId: playerIdFromTrack(teamId, e.trackId),
          teamId,
          jerseyNumber: jersey,
          displayName: e.role === "goalkeeper" ? "GK" : `#${jersey}`,
          role: e.role,
        });
        rawByTrack.set(e.trackId, new Array(totalFrames).fill(null));
      }

      const frds = pitchToFrds(e.x, e.y);
      rawByTrack.get(e.trackId)[local] = { x: frds.x, z: frds.z };

      const stats = trackStats.get(e.trackId) ?? { count: 0, sumX: 0, sumZ: 0, maxSpeed: 0, prev: null };
      stats.count++;
      stats.sumX += frds.x;
      stats.sumZ += frds.z;
      if (stats.prev) {
        const speed = Math.hypot(frds.x - stats.prev.x, frds.z - stats.prev.z) * FPS;
        stats.maxSpeed = Math.max(stats.maxSpeed, speed);
      }
      stats.prev = { x: frds.x, z: frds.z };
      trackStats.set(e.trackId, stats);
    }
  }

  const focus = clipCentroid(trackStats);
  for (const [trackId, stats] of trackStats) {
    stats.avgX = stats.sumX / Math.max(1, stats.count);
    stats.avgZ = stats.sumZ / Math.max(1, stats.count);
    stats.minFrames = totalFrames;
    const visibility = stats.count / totalFrames;
    const focusDist = Math.hypot(stats.avgX - focus.x, stats.avgZ - focus.z);
    stats.score = visibility * 50 + Math.min(stats.maxSpeed, 6) * 3 - focusDist * 0.2;
    trackStats.set(trackId, stats);
  }

  let selectedTracks = pickTopTracks(trackStats, args.maxPlayers, focus.x, focus.z, args.focusRadiusM);
  if (selectedTracks.length < 6) {
    selectedTracks = [...trackStats.entries()]
      .sort((a, b) => b[1].score - a[1].score)
      .slice(0, args.maxPlayers)
      .map(([trackId]) => trackId);
  }
  const selectedPlayerIds = selectedTracks.map((t) => playerMeta.get(t).playerId);

  const filledByTrack = new Map();
  for (const trackId of selectedTracks) {
    filledByTrack.set(trackId, fillTrackGaps(rawByTrack.get(trackId), totalFrames));
  }

  const samplePosition = (playerId, localFrame) => {
    const trackId = selectedTracks[selectedPlayerIds.indexOf(playerId)];
    const p = filledByTrack.get(trackId)[localFrame];
    return p ?? { x: 0, z: 0 };
  };

  const smoothed = trackingMode
    ? buildReplayRootTracks(selectedPlayerIds, samplePosition, totalFrames, FPS)
    : buildSmoothedRootTracks(
        selectedPlayerIds,
        samplePosition,
        totalFrames,
        FPS,
        () => 0,
      );

  const ballPositions = extractBallTrack(detections, args.startFrame, endFrame, totalFrames);
  const trackByPlayerId = new Map(selectedTracks.map((t) => [playerMeta.get(t).playerId, t]));
  const frames = [];

  for (let local = 0; local < totalFrames; local++) {
    const players = selectedPlayerIds.map((playerId) => {
      const trackId = trackByPlayerId.get(playerId);
      const meta = playerMeta.get(trackId);
      const px = smoothed[playerId].x[local];
      const pz = smoothed[playerId].z[local];
      const yaw = smoothed[playerId].yaw[local];
      if (trackingMode) {
        return { playerId, joints: buildJoints(px, pz, yaw, meta.role) };
      }
      const speed = smoothed[playerId].speed[local];
      const phase = smoothed[playerId].phase[local];
      return { playerId, joints: buildJoints(px, pz, yaw, speed, phase, meta.role) };
    });

    frames.push({
      frameIndex: local,
      timestampMs: Math.floor((local * 1000) / FPS),
      ball: { pos: ballPositions[local] },
      players,
    });
  }

  const teams = [
    { teamId: "home", name: "Home", color: "#E63946" },
    { teamId: "away", name: "Away", color: "#457B9D" },
  ];
  const players = selectedTracks.map((trackId) => {
    const m = playerMeta.get(trackId);
    return {
      playerId: m.playerId,
      teamId: m.teamId,
      jerseyNumber: m.jerseyNumber,
      displayName: m.displayName,
    };
  });

  const clipName = path.basename(inputPath, ".json").replace("Labels-GameState", "SNGS-116");
  const manifest = {
    schemaVersion: "0.2.0",
    matchId: args.sequenceId,
    pitch: PITCH,
    timing: { frameRateHz: FPS, durationSeconds: args.durationSec },
    teams,
    players,
    dataSource: {
      type: args.sourceType,
      provider: "SoccerNet Game State Reconstruction",
      clipId: clipName.includes("SNGS") ? clipName : "SNGS-116",
      sourceFile: path.relative(ROOT, inputPath).replace(/\\/g, "/"),
      frameRange: { start: args.startFrame, end: endFrame },
      jointSynthesis: trackingMode ? "tracking_root_marker" : "gait_ik_from_tracking_root",
      smoothing: trackingMode ? "minimal_root_only" : "root_track_moving_average",
      license: "SoccerNet academic / see soccernet.ai",
      note: args.sourceNote,
    },
  };

  const outDirs = [
    path.join(ROOT, "SampleData", args.sequenceId),
    path.join(ROOT, "Assets", "StreamingAssets", args.sequenceId),
  ];

  for (const outDir of outDirs) {
    fs.mkdirSync(outDir, { recursive: true });
    fs.writeFileSync(path.join(outDir, "manifest.json"), JSON.stringify(manifest, null, 2));
    fs.writeFileSync(path.join(outDir, "events.json"), JSON.stringify({ events: [] }, null, 2));
    fs.writeFileSync(
      path.join(outDir, "frames.jsonl"),
      frames.map((f) => JSON.stringify(f)).join("\n") + "\n",
    );
    console.log(
      `Wrote ${outDir} (${frames.length} frames, ${players.length} players, ball=labels, mode=${args.jointMode})`,
    );
  }
}

main();
