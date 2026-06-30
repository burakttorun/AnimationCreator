import fs from "fs";

const path = process.argv[2];
const rs = fs.createReadStream(path, "utf8");
let buf = "";
const ids = new Set();
rs.on("data", (chunk) => {
  buf += chunk;
  const re = /"image_id":\s*"?(\d+)"?/g;
  let m;
  while ((m = re.exec(buf)) !== null) ids.add(m[1]);
  buf = buf.slice(-200);
});
rs.on("end", () => {
  const arr = [...ids].map((x) => BigInt(x)).sort((a, b) => (a < b ? -1 : 1));
  console.log("unique image_ids:", ids.size);
  console.log("first:", arr.slice(0, 5).map(String));
  console.log("last:", arr.slice(-5).map(String));
});
