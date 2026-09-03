import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const workerSource = await readFile(new URL("../src/index.js", import.meta.url), "utf8");
const worker = await import(`data:text/javascript,${encodeURIComponent(workerSource)}`);
const emergencyId = "emergency-autoupdate-2026-05-29";

async function fetchItems(parameters) {
  const url = new URL("https://example.test/news.json");
  for (const [key, value] of Object.entries(parameters)) {
    if (value !== undefined)
      url.searchParams.set(key, value);
  }

  const response = await worker.default.fetch(new Request(url), {});
  assert.equal(response.status, 200);
  return (await response.json()).items;
}

test("locale targeting uses a case-insensitive parent-tag fallback", async () => {
  const regional = await fetchItems({ appVersion: "1.0.173", locale: "ja-JP" });
  const caseVariant = await fetchItems({ appVersion: "1.0.173", locale: "JA-jp" });

  assert.ok(regional.some((item) => item.id === emergencyId));
  assert.ok(caseVariant.some((item) => item.id === emergencyId));
});

test("version-targeted notices are hidden when app version is missing", async () => {
  const known = await fetchItems({ appVersion: "1.0.173", locale: "ja" });
  const missing = await fetchItems({ locale: "ja" });

  assert.ok(known.some((item) => item.id === emergencyId));
  assert.ok(!missing.some((item) => item.id === emergencyId));
});
