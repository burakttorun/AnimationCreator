/**
 * Score frame windows in a SoccerNet GSR JSON for a clean comparison clip.
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { pitchToFrds } from "./lib/pose_from_root.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const FPS = 25;
const PITCH = { lengthM: 105, widthM: 68 };

function loadGsJson(filePath) {
  const raw = fs.readFileSync(filePath, "utf8").replace(/\bNaN\b/g, "null");
  return JSON.parse(raw).predictions ?? [];
}

function frameFromImageId(imageId) {
  return parseInt(String(imageId).slice(-4), 10) - 1;
}

function main() {
  const input = path.join(ROOT, "ExternalData/SoccerNetGS-test/tracklab/SNGS-116.json");
  const detections = loadGsJson(input);
  const byFrame = new Map();

  for (const det of detections) {
    if (det.attributes?.role !== "player" && det.attributes?.role !== "goalkeeper") continue;
    const bp = det.bbox_pitch;
    if (!bp?.x_bottom_middle) continue;
    const fi = frameFromImageId(det.image_id);
    if (!byFrame.has(fi)) byFrame.set(fi, new Map());
    const tracks = byFrame.get(fi);
    tracks.set(det.track_id, pitchToFrds(bp.x_bottom_middle, bp.y_bottom_middle, PITCH));
  }

  const frames = [...byFrame.keys()].sort((a, b) => a - b);
  const windowSec = 8;
  const windowFrames = windowSec * FPS;
  let best = null;

  for (let start = frames[0]; start <= frames[frames.length - 1] - windowFrames; start += 5) {
    const end = start + windowFrames - 1;
    let playerSum = 0;
    let count = 0;
    let totalSpeed = 0;
    let spreadSum = 0;
    const trackCounts = new Map();

    for (let fi = start; fi <= end; fi++) {
      const tracks = byFrame.get(fi);
      if (!tracks || tracks.size < 4) {
        count = 0;
        break;
      }
      playerSum += tracks.size;
      count++;
      const pts = [...tracks.values()];
      const cx = pts.reduce((s, p) => s + p.x, 0) / pts.length;
      const cz = pts.reduce((s, p) => s + p.z, 0) / pts.length;
      const spread = Math.sqrt(pts.reduce((s, p) => s + (p.x - cx) ** 2 + (p.z - cz) ** 2, 0) / pts.length);
      spreadSum += spread;
      for (const [tid] of tracks) trackCounts.set(tid, (trackCounts.get(tid) ?? 0) + 1);
    }

    if (count < windowFrames * 0.75) continue;

    let speedSum = 0;
    let speedN = 0;
    for (const [tid, n] of trackCounts) {
      if (n < windowFrames * 0.7) continue;
      let prev = null;
      for (let fi = start; fi <= end; fi++) {
        const p = byFrame.get(fi)?.get(tid);
        if (!p) continue;
        if (prev) speedSum += Math.hypot(p.x - prev.x, p.z - prev.z) * FPS;
        speedN++;
        prev = p;
      }
    }
    totalSpeed = speedN > 0 ? speedSum / speedN : 0;

    const avgPlayers = playerSum / count;
    const avgSpread = spreadSum / count;
    const stableTracks = [...trackCounts.values()].filter((n) => n >= windowFrames * 0.7).length;

    // Prefer: moderate player count, compact action, visible movement, midfield not corners
    let midX = 0;
    let midZ = 0;
    let midN = 0;
    for (let fi = start; fi <= end; fi += 10) {
      for (const p of byFrame.get(fi)?.values() ?? []) {
        midX += p.x;
        midZ += p.z;
        midN++;
      }
    }
    midX /= Math.max(1, midN);
    midZ /= Math.max(1, midN);
    const centerPenalty = Math.hypot(midX, midZ) * 0.15;

    const score =
      stableTracks * 3 +
      Math.min(totalSpeed, 4) * 8 +
      (avgPlayers >= 6 && avgPlayers <= 12 ? 12 : 0) -
      Math.abs(avgPlayers - 8) * 2 +
      (avgSpread < 18 ? 10 - avgSpread * 0.3 : 0) -
      centerPenalty;

    if (!best || score > best.score) {
      best = { start, end, score, avgPlayers, avgSpread, totalSpeed, stableTracks, midX, midZ };
    }
  }

  console.log(JSON.stringify(best, null, 2));
}

main();
