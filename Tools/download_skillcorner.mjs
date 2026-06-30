/**
 * Download one SkillCorner open-data match via sparse git clone.
 * https://github.com/SkillCorner/opendata
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { spawnSync } from "child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const REPO = "https://github.com/SkillCorner/opendata.git";

function parseArgs(argv) {
  const args = { matchId: "1886347", outRoot: path.join(ROOT, "ExternalData", "SkillCorner") };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--match-id") args.matchId = argv[++i];
    else if (a === "--out") {
      const v = argv[++i];
      args.outRoot = path.isAbsolute(v) ? v : path.join(ROOT, v);
    }
  }
  return args;
}

function run(cmd, cmdArgs, cwd) {
  const r = spawnSync(cmd, cmdArgs, { cwd, stdio: "inherit", shell: process.platform === "win32" });
  if (r.status !== 0) throw new Error(`${cmd} ${cmdArgs.join(" ")} failed (${r.status})`);
}

function main() {
  const { matchId, outRoot } = parseArgs(process.argv);
  const matchDir = path.join(outRoot, "matches", matchId);
  const tracking = path.join(matchDir, `${matchId}_tracking_extrapolated.jsonl`);
  if (fs.existsSync(tracking)) {
    console.log(`Already present: ${tracking}`);
    return;
  }

  const tmp = path.join(outRoot, ".clone-tmp");
  if (fs.existsSync(tmp)) fs.rmSync(tmp, { recursive: true, force: true });
  fs.mkdirSync(outRoot, { recursive: true });

  run("git", ["clone", "--depth", "1", "--filter=blob:none", "--sparse", REPO, tmp], outRoot);
  run("git", ["sparse-checkout", "set", `data/matches/${matchId}`], tmp);
  run("git", ["checkout"], tmp);

  const src = path.join(tmp, "data", "matches", matchId);
  if (!fs.existsSync(src)) throw new Error(`Match ${matchId} not found in repo`);

  fs.mkdirSync(path.dirname(matchDir), { recursive: true });
  fs.cpSync(src, matchDir, { recursive: true });
  fs.rmSync(tmp, { recursive: true, force: true });

  console.log(`SkillCorner match ${matchId} → ${matchDir}`);
}

main();
