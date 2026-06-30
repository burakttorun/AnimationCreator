/**
 * Extract official Labels-GameState.json for a clip from SoccerNet test.zip
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import zlib from "zlib";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const ZIP_URL = "https://exrcsdrive.kaust.edu.sa/public.php/webdav/test.zip";
const AUTH = Buffer.from("iOJmJH6rYnx7mOS:SoccerNet").toString("base64");

const clip = process.argv[2] ?? "SNGS-116";
const OUT_DIR = path.join(ROOT, "ExternalData", "SoccerNetGS", "test", clip);

async function fetchRange(start, end) {
  const res = await fetch(ZIP_URL, {
    headers: { Authorization: `Basic ${AUTH}`, Range: `bytes=${start}-${end}` },
  });
  if (!res.ok && res.status !== 206) throw new Error(`HTTP ${res.status}`);
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
    const zip64Start = tail.length - (zipSize - Number(readUInt64LE(tail, locPos + 8)));
    const zip64 = tail.subarray(zip64Start);
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
  const compressed = await fetchRange(dataStart, dataStart + entry.compSize - 1);
  if (entry.compMethod === 0) return compressed;
  if (entry.compMethod === 8) return zlib.inflateRawSync(compressed);
  throw new Error(`Unsupported compression ${entry.compMethod}`);
}

async function main() {
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const outPath = path.join(OUT_DIR, "Labels-GameState.json");
  if (fs.existsSync(outPath) && fs.statSync(outPath).size > 10000) {
    console.log(`Already present: ${outPath}`);
    return;
  }

  const head = await fetch(ZIP_URL, { method: "HEAD", headers: { Authorization: `Basic ${AUTH}` } });
  const zipSize = Number(head.headers.get("content-length"));
  const tail = await fetchRange(zipSize - 4 * 1024 * 1024, zipSize - 1);
  const { cdSize, cdOffset } = resolveCentralDirectory(tail, zipSize);
  const cdBuf = await fetchRange(cdOffset, cdOffset + cdSize - 1);
  const entries = parseCentralDirectory(cdBuf);
  const candidates = entries
    .map((e) => e.name)
    .filter((n) => n.includes(clip) && n.toLowerCase().includes("labels-gamestate"));
  if (candidates.length === 0) throw new Error(`No Labels-GameState for ${clip}. Found: ${entries.filter((e) => e.name.includes(clip)).map((e) => e.name).slice(0, 10).join(", ")}`);

  const labelName = candidates[0];
  const entry = entries.find((e) => e.name === labelName);
  const raw = await extractEntry(entry);
  fs.writeFileSync(outPath, raw);
  console.log(`Wrote ${outPath} from zip entry ${labelName}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
