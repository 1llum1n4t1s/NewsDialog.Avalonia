// NewsDialog.Avalonia 用 Cloudflare Worker サンプル。
// クライアントが ?appVersion=1.0.172&locale=ja&channel=release を送ってくるので、
// サーバ側で「特定バージョンだけ緊急告知を返す」狙い撃ちができる。
//
// お知らせをコード内に持つ最小例。運用では KV / D1 / R2 に置き換える
// (KV 版は末尾のコメント参照)。

/** @type {Array<Record<string, unknown>>} */
const ANNOUNCEMENTS = [
  {
    id: "emergency-autoupdate-2026-05-29",
    title: "重要: 自動更新の不具合について",
    category: "重要",
    severity: "Emergency",
    isBlocking: true,
    publishedAt: "2026-05-29T03:00:00Z",
    summary:
      "一部バージョンで自動更新が失敗する不具合が判明しました。下のボタンから最新版を手動でダウンロードしてください。",
    actionLabel: "最新版をダウンロード",
    actionUrl: "https://lhamiel.kagayoi.com/",
    // 1.0.173 以下にだけ出す (= 壊れている旧クライアントを狙い撃ち)
    maxAppVersion: "1.0.173",
    locales: ["ja"],
  },
  {
    id: "update-1.0.174",
    title: "アップデート 1.0.174 を公開しました",
    category: "アップデート",
    severity: "Important",
    publishedAt: "2026-05-28T10:00:00Z",
    contentUrl: "https://lhamiel.kagayoi.com/news/1.0.174.html",
  },
];

const MAX_VERSION_COMPONENT = 2_147_483_647;

/** System.Version と同じ 2〜4 個の数値形式を 4 要素へ正規化する。 */
function parseVersion(value) {
  if (typeof value !== "string") return null;

  const parts = value.split(".");
  if (parts.length < 2 || parts.length > 4) return null;

  const components = [];
  for (const part of parts) {
    if (!/^\d+$/.test(part)) return null;

    const component = Number(part);
    if (!Number.isSafeInteger(component) || component > MAX_VERSION_COMPONENT) return null;
    components.push(component);
  }

  while (components.length < 4) components.push(0);
  return components;
}

/** 正規化済みの数値配列を比較。a<b:-1 / a==b:0 / a>b:1 */
function compareVersion(a, b) {
  for (let i = 0; i < a.length; i++) {
    const d = a[i] - b[i];
    if (d !== 0) return d < 0 ? -1 : 1;
  }
  return 0;
}

/** 要求ロケールを親タグへフォールバックして対象タグと比較する。 */
function matchesLocale(requestedLocale, itemLocale) {
  if (typeof requestedLocale !== "string" || typeof itemLocale !== "string") return false;

  const requested = requestedLocale.toLowerCase();
  const candidate = itemLocale.toLowerCase();
  return requested === candidate || (requested.startsWith(candidate) && requested[candidate.length] === "-");
}

function targeted(items, { appVersion, locale }) {
  const now = Date.now();
  const parsedAppVersion = parseVersion(appVersion);

  return items.filter((it) => {
    if (it.expiresAt && Date.parse(it.expiresAt) < now) return false;
    if (typeof locale === "string" && locale.length > 0 && Array.isArray(it.locales) && it.locales.length
        && !it.locales.some((itemLocale) => matchesLocale(locale, itemLocale))) return false;

    const hasMinVersion = typeof it.minAppVersion === "string" && it.minAppVersion.length > 0;
    const hasMaxVersion = typeof it.maxAppVersion === "string" && it.maxAppVersion.length > 0;
    if (hasMinVersion || hasMaxVersion) {
      if (!parsedAppVersion) return false;

      const minVersion = hasMinVersion ? parseVersion(it.minAppVersion) : null;
      const maxVersion = hasMaxVersion ? parseVersion(it.maxAppVersion) : null;
      if ((hasMinVersion && !minVersion) || (hasMaxVersion && !maxVersion)) return false;
      if (minVersion && compareVersion(parsedAppVersion, minVersion) < 0) return false;
      if (maxVersion && compareVersion(parsedAppVersion, maxVersion) > 0) return false;
    }

    return true;
  });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const ctx = {
      appVersion: url.searchParams.get("appVersion"),
      locale: url.searchParams.get("locale"),
      channel: url.searchParams.get("channel"),
    };

    // KV 運用例: const all = JSON.parse(await env.NEWS.get("items")) ?? [];
    const all = ANNOUNCEMENTS;

    const body = JSON.stringify({
      generatedAt: new Date().toISOString(),
      items: targeted(all, ctx),
    });

    return new Response(body, {
      headers: {
        "content-type": "application/json; charset=utf-8",
        "access-control-allow-origin": "*",
        "cache-control": "public, max-age=60",
      },
    });
  },
};
