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
    actionUrl: "https://lhamiel.nephilim.jp/",
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
    contentUrl: "https://lhamiel.nephilim.jp/news/1.0.174.html",
  },
];

/** "1.0.172" 形式を数値配列比較。a<b:-1 / a==b:0 / a>b:1 */
function compareVersion(a, b) {
  const pa = String(a).split(".").map((n) => parseInt(n, 10) || 0);
  const pb = String(b).split(".").map((n) => parseInt(n, 10) || 0);
  const len = Math.max(pa.length, pb.length);
  for (let i = 0; i < len; i++) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d !== 0) return d < 0 ? -1 : 1;
  }
  return 0;
}

function targeted(items, { appVersion, locale }) {
  const now = Date.now();
  return items.filter((it) => {
    if (it.expiresAt && Date.parse(it.expiresAt) < now) return false;
    if (locale && Array.isArray(it.locales) && it.locales.length && !it.locales.includes(locale)) return false;
    if (appVersion) {
      if (it.minAppVersion && compareVersion(appVersion, it.minAppVersion) < 0) return false;
      if (it.maxAppVersion && compareVersion(appVersion, it.maxAppVersion) > 0) return false;
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
