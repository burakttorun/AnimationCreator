import fs from "fs";
import path from "path";
import readline from "readline";
import { fileURLToPath } from "url";
import { pitchToFrds } from "./lib/pose_from_root.mjs";
import { buildTrackingMarkerJoints } from "./lib/tracking_marker.mjs";
import { buildReplayRootTracks, buildReplayBallTrack } from "./lib/root_track.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const BALL_R = 0.11;
const DEFAULT_FPS = 10;

function parseArgs(argv) {
  const args = {
    matchDir: "",
    sequenceId: "skillcorner_demo",
    startFrame: 5000,
    durationSec: 8,
    fps: DEFAULT_FPS,
    maxPlayers: 22,
  };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--match-dir") args.matchDir = argv[++i];
    else if (a === "--sequence-id") args.sequenceId = argv[++i];
    else if (a === "--start-frame") args.startFrame = +argv[++i];
    else if (a === "--duration-sec") args.durationSec = +argv[++i];
    else if (a === "--fps") args.fps = +argv[++i];
    else if (a === "--max-players") args.maxPlayers = +argv[++i];
  }
  if (!args.matchDir) throw new Error("--match-dir required");
  return args;
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

function scToFrds(scX, scY) {
  return pitchToFrds(scX, scY);
}

function cleanBallTrack(raw, totalFrames, pitch, fps) {
  const hw = pitch.widthM * 0.5;
  const hl = pitch.lengthM * 0.5;
  // At 10 Hz a driven pass can move ~4–6 m between samples; only reject obvious teleports.
  const maxStep = Math.max(6, 12 / Math.max(1, fps));
  const cleaned = new Array(totalFrames).fill(null);
  let lastGood = null;

  for (let i = 0; i < totalFrames; i++) {
    const p = raw[i];
    let ok =
      p &&
      Number.isFinite(p.x) &&
      Number.isFinite(p.z) &&
      Math.abs(p.x) <= hw + 2 &&
      Math.abs(p.z) <= hl + 2;
    if (ok && lastGood) {
      const step = Math.hypot(p.x - lastGood.x, p.z - lastGood.z);
      if (step > maxStep) ok = false;
    }
    if (ok) {
      cleaned[i] = { x: p.x, z: p.z, y: p.y };
      lastGood = cleaned[i];
    } else if (lastGood) {
      cleaned[i] = { ...lastGood };
    }
  }

  const first = cleaned.findIndex(Boolean);
  if (first > 0) {
    const seed = cleaned[first];
    for (let i = 0; i < first; i++) cleaned[i] = { ...seed };
  }

  const filled = fillTrackGaps(
    cleaned.map((p) => (p ? { x: p.x, z: p.z } : null)),
    totalFrames,
  );

  const positions = filled.map((p, i) => {
    const h = cleaned[i]?.y ?? BALL_R;
    return p
      ? { x: p.x, y: h > 0 ? Math.max(BALL_R, h) : BALL_R, z: p.z }
      : { x: 0, y: BALL_R, z: 0 };
  });

  return buildReplayBallTrack(positions, fps, { window: 0 });
}

function loadMatchMeta(matchDir, matchId) {
  const matchPath = path.join(matchDir, `${matchId}_match.json`);
  const match = JSON.parse(fs.readFileSync(matchPath, "utf8"));
  const playerById = new Map();
  for (const p of match.players ?? []) {
    if (p.player_role?.name === "Substitute" && !p.playing_time?.total) continue;
    const teamId = p.team_id === match.home_team.id ? "home" : "away";
    const role = p.player_role?.name === "Goalkeeper" ? "goalkeeper" : "player";
    playerById.set(p.id, {
      playerId: `p${p.id}`,
      teamId,
      jerseyNumber: p.number,
      displayName: p.player_role?.name === "Goalkeeper" ? "GK" : p.short_name ?? `#${p.number}`,
      role,
      scId: p.id,
    });
  }
  return {
    match,
    playerById,
    pitch: { lengthM: match.pitch_length ?? 105, widthM: match.pitch_width ?? 68 },
    teams: [
      {
        teamId: "home",
        name: match.home_team?.name ?? "Home",
        color: match.home_team_kit?.jersey_color ?? "#E63946",
      },
      {
        teamId: "away",
        name: match.away_team?.name ?? "Away",
        color: match.away_team_kit?.jersey_color ?? "#457B9D",
      },
    ],
  };
}

async function loadClipFrames(trackingPath, startFrame, endFrame) {
  const frames = [];
  const rl = readline.createInterface({
    input: fs.createReadStream(trackingPath, { encoding: "utf8" }),
    crlfDelay: Infinity,
  });

  for await (const line of rl) {
    if (!line.trim()) continue;
    const row = JSON.parse(line);
    if (row.frame < startFrame) continue;
    if (row.frame > endFrame) break;
    frames.push(row);
  }
  rl.close();
  return frames;
}

function mapEventType(scType, scSubtype) {
  const t = String(scType ?? "").toLowerCase();
  const st = String(scSubtype ?? "").toLowerCase();
  if (t.includes("shot")) return "shot";
  if (st === "pass" || t.includes("pass")) return "pass";
  if (t.includes("cross")) return "pass";
  return null;
}

function loadEventsCsv(csvPath, startFrame, endFrame, fps, playerById) {
  if (!fs.existsSync(csvPath)) return [];
  const text = fs.readFileSync(csvPath, "utf8");
  const lines = text.split(/\r?\n/).filter(Boolean);
  if (lines.length < 2) return [];

  const header = lines[0].split(",");
  const col = (name) => header.indexOf(name);
  const events = [];
  let n = 0;

  for (let i = 1; i < lines.length; i++) {
    const parts = lines[i].split(",");
    const frameStart = +parts[col("frame_start")];
    if (!Number.isFinite(frameStart) || frameStart < startFrame || frameStart > endFrame) continue;

    const scType = parts[col("event_type")];
    const scSubtype = parts[col("event_subtype")];
    const frdsType = mapEventType(scType, scSubtype);
    if (!frdsType) continue;

    const scPlayerId = +parts[col("player_id")];
    const meta = playerById.get(scPlayerId);
    const localFrame = frameStart - startFrame;
    const targetScId = +parts[col("player_targeted_id")];
    const targetMeta = Number.isFinite(targetScId) ? playerById.get(targetScId) : null;

    events.push({
      eventId: `sc_${++n}`,
      type: frdsType,
      frameIndex: localFrame,
      actorPlayerId: meta?.playerId ?? `p${scPlayerId}`,
      ...(targetMeta ? { targetPlayerId: targetMeta.playerId } : {}),
      timestampMs: Math.floor((localFrame * 1000) / fps),
    });
  }
  return events.slice(0, 80);
}

async function main() {
  const args = parseArgs(process.argv);
  const matchDir = path.isAbsolute(args.matchDir) ? args.matchDir : path.join(ROOT, args.matchDir);
  const matchId = path.basename(matchDir);
  const trackingPath = path.join(matchDir, `${matchId}_tracking_extrapolated.jsonl`);
  if (!fs.existsSync(trackingPath)) {
    throw new Error(`Missing tracking file: ${trackingPath}`);
  }

  const { match, playerById, pitch, teams } = loadMatchMeta(matchDir, matchId);
  const endFrame = args.startFrame + Math.floor(args.durationSec * args.fps) - 1;
  const totalFrames = endFrame - args.startFrame + 1;
  const clipRows = await loadClipFrames(trackingPath, args.startFrame, endFrame);

  if (clipRows.length < 5) {
    throw new Error(`Too few tracking frames in range (${clipRows.length})`);
  }

  const activePlayerIds = new Set();
  const rawByPlayer = new Map();
  const rawBall = new Array(totalFrames).fill(null);

  for (const row of clipRows) {
    const local = row.frame - args.startFrame;
    if (local < 0 || local >= totalFrames) continue;

    const ball = row.ball_data;
    if (ball && Number.isFinite(ball.x) && Number.isFinite(ball.y)) {
      const frds = scToFrds(ball.x, ball.y);
      rawBall[local] = { x: frds.x, z: frds.z, y: Number.isFinite(ball.z) ? ball.z : BALL_R };
    }

    for (const pd of row.player_data ?? []) {
      if (!Number.isFinite(pd.x) || !Number.isFinite(pd.y)) continue;
      const meta = playerById.get(pd.player_id);
      if (!meta) continue;

      activePlayerIds.add(meta.playerId);
      if (!rawByPlayer.has(meta.playerId)) {
        rawByPlayer.set(meta.playerId, new Array(totalFrames).fill(null));
      }
      const frds = scToFrds(pd.x, pd.y);
      rawByPlayer.get(meta.playerId)[local] = { x: frds.x, z: frds.z };
    }
  }

  const selectedPlayerIds = [...activePlayerIds].slice(0, args.maxPlayers);
  const filledByPlayer = new Map();
  for (const playerId of selectedPlayerIds) {
    filledByPlayer.set(playerId, fillTrackGaps(rawByPlayer.get(playerId) ?? new Array(totalFrames).fill(null), totalFrames));
  }

  const samplePosition = (playerId, localFrame) => {
    const p = filledByPlayer.get(playerId)?.[localFrame];
    return p ?? { x: 0, z: 0 };
  };

  const smoothed = buildReplayRootTracks(selectedPlayerIds, samplePosition, totalFrames, args.fps);
  const ballPositions = cleanBallTrack(rawBall, totalFrames, pitch, args.fps);

  const frames = [];
  for (let local = 0; local < totalFrames; local++) {
    const players = selectedPlayerIds.map((playerId) => {
      const meta = [...playerById.values()].find((m) => m.playerId === playerId);
      const px = smoothed[playerId].x[local];
      const pz = smoothed[playerId].z[local];
      const yaw = smoothed[playerId].yaw[local];
      return {
        playerId,
        joints: buildTrackingMarkerJoints(px, pz, yaw, meta?.role ?? "player"),
      };
    });

    frames.push({
      frameIndex: local,
      timestampMs: Math.floor((local * 1000) / args.fps),
      ball: { pos: ballPositions[local] },
      players,
    });
  }

  const players = selectedPlayerIds.map((playerId) => {
    const meta = [...playerById.values()].find((m) => m.playerId === playerId);
    return {
      playerId,
      teamId: meta.teamId,
      jerseyNumber: meta.jerseyNumber,
      displayName: meta.displayName,
    };
  });

  const eventsPath = path.join(matchDir, `${matchId}_dynamic_events.csv`);
  const events = loadEventsCsv(eventsPath, args.startFrame, endFrame, args.fps, playerById);

  const manifest = {
    schemaVersion: "0.2.0",
    matchId: args.sequenceId,
    pitch,
    timing: { frameRateHz: args.fps, durationSeconds: args.durationSec },
    teams,
    players,
    dataSource: {
      type: "skillcorner_tracking",
      provider: "SkillCorner Open Data",
      clipId: String(matchId),
      sourceFile: path.relative(ROOT, trackingPath).replace(/\\/g, "/"),
      frameRange: { start: args.startFrame, end: endFrame },
      jointSynthesis: "tracking_root_marker",
      smoothing: "minimal_root_only",
      license: "SkillCorner open data / see github.com/SkillCorner/opendata",
      note: `${match.home_team?.name ?? "Home"} vs ${match.away_team?.name ?? "Away"} — frames ${args.startFrame}–${endFrame} @ ${args.fps} Hz`,
    },
  };

  const outDirs = [
    path.join(ROOT, "SampleData", args.sequenceId),
    path.join(ROOT, "Assets", "StreamingAssets", args.sequenceId),
  ];

  for (const outDir of outDirs) {
    fs.mkdirSync(outDir, { recursive: true });
    fs.writeFileSync(path.join(outDir, "manifest.json"), JSON.stringify(manifest, null, 2));
    fs.writeFileSync(path.join(outDir, "events.json"), JSON.stringify({ events }, null, 2));
    fs.writeFileSync(
      path.join(outDir, "frames.jsonl"),
      frames.map((f) => JSON.stringify(f)).join("\n") + "\n",
    );
    console.log(
      `Wrote ${outDir} (${frames.length} frames @ ${args.fps} Hz, ${players.length} players, ${events.length} events)`,
    );
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
