/**
 * Extract SNGS-116 broadcast frames from SoccerNet gamestate-2024 test.zip
 * and optionally assemble video.mkv with ffmpeg.
 *
 * Usage:
 *   node Tools/download_sngs116_video.mjs
 *   node Tools/download_sngs116_video.mjs --start 240 --count 200
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { spawnSync } from "child_process";
import zlib from "zlib";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const ZIP_URL = "https://exrcsdrive.kaust.edu.sa/public.php/webdav/test.zip";
const AUTH = Buffer.from("iOJmJH6rYnx7mOS:SoccerNet").toString("base64");
const CLIP = "SNGS-116";
const OUT_DIR = path.join(ROOT, "ExternalData", "SoccerNetGS", "test", CLIP);
const FRAMES_DIR = path.join(OUT_DIR, "frames");

function parseArgs() {
  const args = { start: 375, count: 200, clearStale: true };
  for (let i = 2; i < process.argv.length; i++) {
    if (process.argv[i] === "--start") args.start = +process.argv[++i];
    else if (process.argv[i] === "--count") args.count = +process.argv[++i];
    else if (process.argv[i] === "--no-clear-stale") args.clearStale = false;
  }
  return args;
}

async function fetchRange(start, end) {
  const res = await fetch(ZIP_URL, {
    headers: { Authorization: `Basic ${AUTH}`, Range: `bytes=${start}-${end}` },
  });
  if (!res.ok && res.status !== 206) throw new Error(`HTTP ${res.status} for ${start}-${end}`);
  return Buffer.from(await res.arrayBuffer());
}

function findEocd(buf) {
  for (let i = buf.length - 22; i >= 0; i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) return i;
  }
  throw new Error("EOCD not found");
}

function readUInt64LE(buf, offset) {
  return buf.readUInt32LE(offset + 4) * 0x100000000 + buf.readUInt32LE(offset);
}

function resolveCentralDirectory(tail, zipSize) {
  const eocdPos = findEocd(tail);
  const eocd = tail.subarray(eocdPos);
  let cdSize = eocd.readUInt32LE(12);
  let cdOffset = eocd.readUInt32LE(16);
  if (cdSize === 0xffffffff || cdOffset === 0xffffffff) {
    let locPos = -1;
    for (let i = eocdPos - 4; i >= 0; i--) {
      if (tail.readUInt32LE(i) === 0x07064b50) {
        locPos = i;
        break;
      }
    }
    if (locPos < 0) throw new Error("Zip64 locator not found");
    const zip64EocdOffset = readUInt64LE(tail, locPos + 8);
    const zip64StartInTail = tail.length - (zipSize - zip64EocdOffset);
    if (zip64StartInTail < 0) throw new Error("Zip64 EOCD outside tail window");
    const zip64 = tail.subarray(zip64StartInTail);
    cdSize = Number(readUInt64LE(zip64, 40));
    cdOffset = Number(readUInt64LE(zip64, 48));
  }
  return { cdSize, cdOffset };
}

function parseCentralDirectory(cdBuf) {
  const entries = [];
  let p = 0;
  while (p + 46 <= cdBuf.length) {
    if (cdBuf.readUInt32LE(p) !== 0x02014b50) break;
    const compMethod = cdBuf.readUInt16LE(p + 10);
    let compSize = cdBuf.readUInt32LE(p + 20);
    let uncompSize = cdBuf.readUInt32LE(p + 24);
    const nameLen = cdBuf.readUInt16LE(p + 28);
    const extraLen = cdBuf.readUInt16LE(p + 30);
    const commentLen = cdBuf.readUInt16LE(p + 32);
    let localOffset = cdBuf.readUInt32LE(p + 42);
    const name = cdBuf.toString("utf8", p + 46, p + 46 + nameLen);
    const extraStart = p + 46 + nameLen;
    const extraEnd = extraStart + extraLen;
    if (extraLen > 0) {
      let e = extraStart;
      while (e + 4 <= extraEnd) {
        const headerId = cdBuf.readUInt16LE(e);
        const dataSize = cdBuf.readUInt16LE(e + 2);
        if (headerId === 0x0001) {
          let off = e + 4;
          if (uncompSize === 0xffffffff) {
            uncompSize = Number(readUInt64LE(cdBuf, off));
            off += 8;
          }
          if (compSize === 0xffffffff) {
            compSize = Number(readUInt64LE(cdBuf, off));
            off += 8;
          }
          if (localOffset === 0xffffffff) localOffset = Number(readUInt64LE(cdBuf, off));
        }
        e += 4 + dataSize;
      }
    }
    entries.push({ name, compMethod, compSize, uncompSize, localOffset });
    p += 46 + nameLen + extraLen + commentLen;
  }
  return entries;
}

async function extractEntry(entry) {
  const localHdr = await fetchRange(entry.localOffset, entry.localOffset + 1023);
  const nameLen = localHdr.readUInt16LE(26);
  const extraLen = localHdr.readUInt16LE(28);
  const dataStart = entry.localOffset + 30 + nameLen + extraLen;

  const chunk = 4 * 1024 * 1024;
  const parts = [];
  let downloaded = 0;
  while (downloaded < entry.compSize) {
    const from = dataStart + downloaded;
    const to = Math.min(dataStart + downloaded + chunk - 1, dataStart + entry.compSize - 1);
    parts.push(await fetchRange(from, to));
    downloaded += to - from + 1;
  }
  const compressed = Buffer.concat(parts);
  if (entry.compMethod === 0) return compressed;
  if (entry.compMethod === 8) return zlib.inflateRawSync(compressed);
  throw new Error(`Unsupported compression ${entry.compMethod} for ${entry.name}`);
}

function frameName(frameIndex) {
  return String(frameIndex + 1).padStart(6, "0") + ".jpg";
}

function findFfmpeg() {
  const candidates = ["ffmpeg", "C:\\ffmpeg\\bin\\ffmpeg.exe"];
  const local = process.env.LOCALAPPDATA
    ? path.join(process.env.LOCALAPPDATA, "Microsoft", "WinGet", "Links", "ffmpeg.exe")
    : null;
  if (local) candidates.unshift(local);
  for (const c of candidates) {
    const r = spawnSync(c, ["-version"], { stdio: "ignore" });
    if (r.status === 0) return c;
  }
  return null;
}

async function main() {
  const { start, count, clearStale } = parseArgs();
  const end = start + count - 1;
  const needed = [];
  for (let f = start; f <= end; f++) needed.push(`${CLIP}/img1/${frameName(f)}`);

  fs.mkdirSync(FRAMES_DIR, { recursive: true });
  if (clearStale) {
    const neededNames = new Set(needed.map((n) => path.basename(n)));
    for (const file of fs.readdirSync(FRAMES_DIR)) {
      if (file.endsWith(".jpg") && !neededNames.has(file)) {
        fs.unlinkSync(path.join(FRAMES_DIR, file));
      }
    }
  }
  const existing = needed.filter((n) => {
    const local = path.join(FRAMES_DIR, path.basename(n));
    return fs.existsSync(local) && fs.statSync(local).size > 1000;
  });
  if (existing.length === needed.length) {
    console.log(`Frames already present (${existing.length}) in ${FRAMES_DIR}`);
  } else {
    console.log(`Loading zip index...`);
    const head = await fetch(ZIP_URL, { method: "HEAD", headers: { Authorization: `Basic ${AUTH}` } });
    const zipSize = Number(head.headers.get("content-length"));
    const tail = await fetchRange(zipSize - 4 * 1024 * 1024, zipSize - 1);
    const { cdSize, cdOffset } = resolveCentralDirectory(tail, zipSize);
    const cdBuf = await fetchRange(cdOffset, cdOffset + cdSize - 1);
    const entries = parseCentralDirectory(cdBuf);
    const byName = new Map(entries.map((e) => [e.name, e]));

    let i = 0;
    for (const name of needed) {
      const entry = byName.get(name);
      if (!entry) throw new Error(`Missing in zip: ${name}`);
      const outPath = path.join(FRAMES_DIR, path.basename(name));
      if (fs.existsSync(outPath) && fs.statSync(outPath).size > 1000) continue;
      const raw = await extractEntry(entry);
      fs.writeFileSync(outPath, raw);
      i++;
      if (i % 20 === 0) process.stdout.write(`\rExtracted ${i}/${needed.length}`);
    }
    console.log(`\nWrote ${needed.length} frames to ${FRAMES_DIR}`);
  }

  const listFile = path.join(OUT_DIR, "frames_concat.txt");
  const lines = [];
  for (let f = start; f <= end; f++) {
    const p = path.join(FRAMES_DIR, frameName(f)).replace(/\\/g, "/");
    lines.push(`file '${p}'`, "duration 0.04");
  }
  const last = path.join(FRAMES_DIR, frameName(end)).replace(/\\/g, "/");
  lines.push(`file '${last}'`);
  fs.writeFileSync(listFile, lines.join("\n"));

  const videoOut = path.join(OUT_DIR, "video.mkv");
  const ffmpeg = findFfmpeg();
  if (!ffmpeg) {
    console.log("ffmpeg not found — frames ready; Unity will play image sequence.");
    return;
  }

  console.log("Assembling video.mkv with ffmpeg...");
  const r = spawnSync(
    ffmpeg,
    ["-y", "-f", "concat", "-safe", "0", "-i", listFile, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "25", videoOut],
    { stdio: "inherit" },
  );
  if (r.status !== 0) throw new Error("ffmpeg failed");
  console.log(`Wrote ${videoOut}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
