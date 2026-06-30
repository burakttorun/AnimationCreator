/**
 * Download SoccerTrack v2 GSR + BAS JSON (no video) from Hugging Face.
 * Requires free HF account + access token (dataset is gated).
 *
 *   set HF_TOKEN=hf_...
 *   node Tools/download_soccertrack_gsr.mjs --match 117093
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const REPO = "atomscott/soccertrack-v2";

function parseArgs() {
  const args = { match: "117093", dest: path.join(ROOT, "ExternalData", "soccertrack-v2") };
  for (let i = 2; i < process.argv.length; i++) {
    if (process.argv[i] === "--match") args.match = process.argv[++i];
    else if (process.argv[i] === "--dest") args.dest = process.argv[++i];
  }
  return args;
}

async function downloadFile(relPath, destPath, token) {
  const url = `https://huggingface.co/datasets/${REPO}/resolve/main/${relPath}`;
  const res = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    redirect: "follow",
  });
  if (!res.ok) throw new Error(`HTTP ${res.status} for ${relPath}`);
  fs.mkdirSync(path.dirname(destPath), { recursive: true });
  const buf = Buffer.from(await res.arrayBuffer());
  fs.writeFileSync(destPath, buf);
  console.log(`OK ${relPath} (${buf.length} bytes)`);
}

async function main() {
  const { match, dest } = parseArgs();
  const token = process.env.HF_TOKEN || process.env.HUGGING_FACE_HUB_TOKEN;
  if (!token) {
    console.error("Set HF_TOKEN (huggingface.co/settings/tokens) and accept dataset terms at:");
    console.error(`https://huggingface.co/datasets/${REPO}`);
    process.exit(1);
  }
  await downloadFile(`gsr/${match}/${match}_1st.json`, path.join(dest, "gsr", match, `${match}_1st.json`), token);
  await downloadFile(`bas/${match}/${match}_12_class_events.json`, path.join(dest, "bas", match, `${match}_12_class_events.json`), token);
  console.log(`\nThen convert: node Tools/convert_soccertrack_to_frds.mjs --match ${match}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
