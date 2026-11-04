import test, { after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, writeFile, readFile, copyFile } from 'node:fs/promises';
import { join, dirname, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { createServer } from 'node:http';
import { createHash, randomUUID } from 'node:crypto';

const exec = promisify(execFile);
const here = dirname(fileURLToPath(import.meta.url));
const entry = join(here, 'Publish-CloudflareWorker.ps1');
const pwsh = process.env.WORKER_PUBLISHER_PWSH || 'pwsh';
// WORKER_PUBLISHER_TEST_ROOTに専用の絶対パスを指定し、bun test --timeout 60000 <このファイル>。
// 各実行のfixtureは専用子directoryに保持する。Windowsのローカル作業ではD:を使い、確認後にPrunletで清掃する。
const baseRoot = process.env.WORKER_PUBLISHER_TEST_ROOT;
assert.ok(baseRoot && isAbsolute(baseRoot), 'Set WORKER_PUBLISHER_TEST_ROOT to a dedicated absolute work directory');
const root = join(baseRoot, `worker-publisher-${randomUUID()}`);
await mkdir(root);
const quote = x => `'${x.replaceAll("'", "''")}'`;
const options = { maxBuffer: 8 * 1024 * 1024, env: { ...process.env, TEMP: root, TMP: root, POWERSHELL_TELEMETRY_OPTOUT: '1', PSModuleAnalysisCachePath: join(root, 'ModuleAnalysisCache'), CLOUDFLARE_API_TOKEN: '', CLOUDFLARE_ACCOUNT_ID: '' } };
let unexpectedTransports = 0;
// assert.rejectsで子プロセスの失敗を期待するケースも、通信ガードの失敗を飲み込ませない。
after(() => assert.equal(unexpectedTransports, 0, 'Publisher fixture attempted non-loopback HTTP transport'));
async function runPowerShell(command, expectedBlocked = null) {
  // 子プロセスごとに所有するsinkで外向きHTTP/CONNECTを拒否する。宛先・header・bodyは記録しない。
  const blocked = [], sockets = new Set();
  const sink = createServer((req, res) => {
    blocked.push(req.method);
    res.writeHead(403, { Connection: 'close' }); res.end();
  });
  sink.on('connection', socket => { sockets.add(socket); socket.on('close', () => sockets.delete(socket)); });
  sink.on('connect', (req, socket) => {
    blocked.push('CONNECT');
    socket.end('HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n');
  });
  await new Promise((resolve, reject) => { sink.once('error', reject); sink.listen(0, '127.0.0.1', resolve); });
  const proxy = `http://127.0.0.1:${sink.address().port}`;
  const env = { ...options.env };
  for (const key of ['HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY']) env[key] = env[key.toLowerCase()] = proxy;
  env.NO_PROXY = env.no_proxy = '127.0.0.1,localhost,::1';
  try {
    return await exec(pwsh, ['-NoProfile', '-Command', command], { ...options, env, timeout: 45000 });
  } finally {
    const closed = new Promise(resolve => sink.close(resolve));
    for (const socket of sockets) socket.destroy();
    await closed;
    if (expectedBlocked) assert.deepEqual(blocked, expectedBlocked);
    if (blocked.length) {
      if (!expectedBlocked) unexpectedTransports += blocked.length;
      const error = new Error(`Fixture blocked non-loopback HTTP transport: ${blocked.join(', ')}`);
      error.code = 'ERR_FIXTURE_NETWORK'; throw error;
    }
  }
}
async function invoke(config, args = '', script = entry) {
  // PowerShell 7.0のhostエラー表示はBunのpipeで失われるため、例外を標準stderrへ明示する。
  const command = `try { & ${quote(script)} -ConfigPath ${quote(config)} ${args} | ConvertTo-Json -Depth 100 -Compress } catch { [Console]::Error.WriteLine($_.Exception.GetBaseException().Message); exit 1 }`;
  try {
    const { stdout } = await runPowerShell(command);
    return JSON.parse(stdout.trim());
  } catch (cause) {
    if (cause.code === 'ERR_FIXTURE_NETWORK') throw cause;
    const error = new Error(cause.stderr || 'PowerShell child failed without diagnostic');
    error.stderr = cause.stderr; error.code = cause.code; throw error;
  }
}
test('child HTTP and HTTPS use the rejecting sink and unexpected transport fails the fixture', async () => {
  // .invalid宛先はproxy内で403になり、外部DNS/TLS/HTTP接続を行わない。
  const command = `$client = [Net.Http.HttpClient]::new(); try { $client.GetAsync('http://publisher-network-disabled.invalid').GetAwaiter().GetResult().Dispose(); try { $client.GetAsync('https://publisher-network-disabled.invalid').GetAwaiter().GetResult().Dispose() } catch {} } finally { $client.Dispose() }`;
  await assert.rejects(runPowerShell(command, ['GET', 'CONNECT']), error => error.code === 'ERR_FIXTURE_NETWORK');
});
async function fixture(name, extra = {}) {
  const dir = join(root, name); await mkdir(join(dir, 'assets'), { recursive: true });
  await writeFile(join(dir, 'main.js'), `import data from './data.json' with { type: 'json' }; export default {fetch(){return new Response(data.x)}};\n//# sourceMappingURL=main.js.map\n`);
  await writeFile(join(dir, 'data.json'), '{"x":"ok"}');
  await writeFile(join(dir, 'main.js.map'), '{"version":3,"sources":[]}');
  await writeFile(join(dir, 'assets', 'index.html'), '<p>test</p>');
  await writeFile(join(dir, 'assets', '_headers'), '/*\n  X-Test: yes\n');
  const config = { name: 'test-worker', main: './main.js', compatibility_date: '2026-05-01', ...extra };
  await writeFile(join(dir, 'wrangler.jsonc'), JSON.stringify(config));
  return { dir, config: join(dir, 'wrangler.jsonc') };
}
async function mock(handler, action) {
  const requests = [];
  const server = createServer(async (req, res) => {
    const chunks = []; for await (const chunk of req) chunks.push(chunk);
    const item = { method: req.method, path: req.url, headers: req.headers, body: Buffer.concat(chunks).toString() };
    requests.push(item);
    const response = handler(item, requests);
    res.writeHead(response.status ?? 200, response.headers ?? { 'Content-Type': 'application/json' });
    res.end(response.raw ?? JSON.stringify({ success: true, result: response.result ?? {} }));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try { await action(`http://127.0.0.1:${server.address().port}/v4`, requests); }
  finally { await new Promise(resolve => server.close(resolve)); }
}
test('actual entry prepares modules, maps, bindings and assets without credentials or network', async () => {
  const { config } = await fixture('prepare', { assets: { directory: './assets', binding: 'ASSETS' }, vars: { TEXT: 'hello', JSON: { count: 2 } }, workflows: [{ name: 'wf', binding: 'WF', class_name: 'Workflow' }] });
  const result = await invoke(config, '-DryRun');
  assert.deepEqual(result.Modules, ['main.js', 'data.json']); assert.deepEqual(result.SourceMaps, ['main.js.map']);
  assert.equal(result.AssetCount, 1); assert.equal(result.Metadata.assets.config._headers, '/*\n  X-Test: yes\n');
  const expected = createHash('sha256').update(Buffer.from('<p>test</p>').toString('base64') + 'html').digest('hex').slice(0, 32);
  assert.equal(result.Manifest['/index.html'].hash, expected);
  assert.equal(result.Metadata.bindings.find(x => x.name === 'WF').type, 'workflow');
});
test('TOML multiline arrays/tables and JSONC remain accepted; invalid config fails before HTTP', async () => {
  const f = await fixture('toml');
  const toml = join(f.dir, 'wrangler.toml');
  await writeFile(toml, `name = 'test-worker'\nmain = "./main.js"\ncompatibility_date = "2026-05-01"\ncompatibility_flags = [\n "nodejs_compat", # comment\n]\n[vars]\nURL = "https://example.invalid/#anchor"\n[[d1_databases]]\nbinding = 'DB'\ndatabase_id = 'db-id'\n`);
  const result = await invoke(toml, '-DryRun');
  assert.equal(result.Metadata.bindings.find(x => x.name === 'DB').id, 'db-id');
  assert.equal(result.Metadata.bindings.find(x => x.name === 'URL').text, 'https://example.invalid/#anchor');
  await writeFile(f.config, '// comment\n{"name":"x","main":"main.js","compatibility_date":"2026-05-01",}');
  assert.equal((await invoke(f.config, '-DryRun')).Worker, 'x');
  const invalid = await fixture('invalid', { keep_vars: true });
  await assert.rejects(invoke(invalid.config, '-DryRun'), /keep_vars/);
});
test('publish entry preflights routes, secrets, workflows and migration before uploading assets and worker', async () => {
  const f = await fixture('publish', { assets: { directory: './assets' }, routes: [{ pattern: 'test.invalid/*', zone_id: 'zone' }], secrets: { required: ['REQUIRED'] }, migrations: [{ tag: 'v1', new_classes: ['Old'] }, { tag: 'v2', new_sqlite_classes: ['New'] }], workflows: [{ name: 'wf', binding: 'WF', class_name: 'Workflow', schedules: ['* * * * *'] }], triggers: { crons: [] } });
  const hash = createHash('sha256').update(Buffer.from('<p>test</p>').toString('base64') + 'html').digest('hex').slice(0, 32);
  await mock(req => {
    if (req.path.endsWith('/routes')) return { result: [] };
    if (req.path.endsWith('/secrets')) return { result: [{ name: 'REQUIRED' }] };
    if (req.path.includes('/workers/services/')) return { result: { default_environment: { script: { migration_tag: 'v1' } } } };
    if (req.path.endsWith('/assets-upload-session')) return { result: { jwt: 'asset-jwt', buckets: [[hash]] } };
    if (req.path.includes('/assets/upload')) return { result: { jwt: 'completed-jwt' } };
    if (req.path.endsWith('/workflows/wf') && req.method === 'GET') return { status: 404 };
    return { result: { id: 'worker-version' } };
  }, async (url, requests) => {
    assert.equal((await invoke(f.config, `-ApiBase ${quote(url)} -AccountId account -ApiToken fixture-token`)).Success, true);
    const upload = requests.find(x => x.method === 'PUT' && x.path.endsWith('/scripts/test-worker'));
    assert.ok(upload); assert.ok(upload.body.includes('completed-jwt')); assert.ok(upload.body.includes('"old_tag":"v1"'));
    assert.ok(upload.body.includes('"new_sqlite_classes":["New"]')); assert.ok(!upload.body.includes('"new_classes":["Old"]'));
    assert.ok(upload.body.includes('export default {"x":"ok"};')); assert.ok(!upload.body.includes('with { type:'));
    const asset = requests.find(x => x.path.includes('/assets/upload'));
    assert.equal(asset.headers.authorization, 'Bearer asset-jwt');
    const firstWrite = requests.findIndex(x => x.method !== 'GET');
    assert.ok(requests.slice(0, firstWrite).some(x => x.path.endsWith('/secrets')));
    assert.equal(requests.at(-1).method, 'POST'); assert.ok(requests.at(-1).path.endsWith('/routes'));
  });
});
test('conflict and missing secret stop before any mutation', async () => {
  for (const mode of ['conflict', 'secret']) {
    const f = await fixture(mode, { routes: [{ pattern: 'test.invalid/*', zone_id: 'zone' }], secrets: { required: ['REQUIRED'] } });
    await mock(req => req.path.endsWith('/routes') ? { result: mode === 'conflict' ? [{ pattern: 'test.invalid/*', script: 'other' }] : [] } : { result: [] }, async (url, requests) => {
      await assert.rejects(invoke(f.config, `-ApiBase ${quote(url)} -AccountId account -ApiToken fixture-token`));
      assert.ok(requests.every(x => x.method === 'GET'));
    });
  }
});
test('redirect, invalid JSON, failure after worker upload: no retry and no bearer/body leakage', async () => {
  for (const mode of ['redirect', 'invalid-json', 'partial']) {
    const f = await fixture(mode);
    await mock(req => {
      if (mode === 'redirect') return { status: 302, headers: { Location: 'http://127.0.0.1:1/never' }, raw: 'fixture-token' };
      if (mode === 'invalid-json') return { raw: 'fixture-token' };
      if (req.path.endsWith('/subdomain')) return { status: 500, raw: 'fixture-token' };
      return { result: { id: 'published-before-later-failure' } };
    }, async (url, requests) => {
      try { await invoke(f.config, `-ApiBase ${quote(url)} -AccountId account -ApiToken fixture-token`); assert.fail('must fail'); }
      catch (error) { assert.ok(!error.stderr?.includes('fixture-token')); }
      assert.equal(requests.filter(x => x.method === 'PUT').length, 1);
      assert.equal(requests.length, mode === 'partial' ? 2 : 1);
    });
  }
});
test('source-hash namespaces allow repeated calls and different core revisions in one host', async () => {
  const f = await fixture('reload');
  const second = join(f.dir, 'second'); await mkdir(second, { recursive: true });
  await copyFile(entry, join(second, 'Publish-CloudflareWorker.ps1'));
  const source = await readFile(join(here, 'CloudflareWorkerPublisher.cs'), 'utf8');
  await writeFile(join(second, 'CloudflareWorkerPublisher.cs'), source.replaceAll('{"Worker",name}', '{"Worker",name+"-revision"}'));
  const script = `& ${quote(entry)} -ConfigPath ${quote(f.config)} -DryRun | Out-Null; & ${quote(entry)} -ConfigPath ${quote(f.config)} -DryRun | Out-Null; & ${quote(join(second, 'Publish-CloudflareWorker.ps1'))} -ConfigPath ${quote(f.config)} -DryRun | ConvertTo-Json -Depth 100 -Compress`;
  const { stdout } = await runPowerShell(script);
  assert.equal(JSON.parse(stdout.trim()).Worker, 'test-worker-revision');
});
test('module and source-map boundaries, explicit manifest, binary module and synthetic secret redaction', async () => {
  const f = await fixture('boundaries');
  await writeFile(join(f.dir, 'secrets.json'), '{"SYNTHETIC":"sensitive-fixture"}');
  const dry = await invoke(f.config, `-DryRun -SecretsFile ${quote(join(f.dir, 'secrets.json'))}`);
  assert.equal(dry.Metadata.bindings.find(x => x.name === 'SYNTHETIC').text, '[redacted]');
  assert.ok(!JSON.stringify(dry).includes('sensitive-fixture'));
  await writeFile(join(f.dir, 'binary.wasm'), Buffer.from([0, 97, 115, 109]));
  await writeFile(join(f.dir, 'modules.json'), '["binary.wasm"]');
  const result = await invoke(f.config, `-DryRun -ModuleManifestPath ${quote(join(f.dir, 'modules.json'))}`);
  assert.deepEqual(result.Modules, ['main.js', 'binary.wasm']);
  await writeFile(join(f.dir, 'modules.json'), '["../escape.js"]');
  await assert.rejects(invoke(f.config, `-DryRun -ModuleManifestPath ${quote(join(f.dir, 'modules.json'))}`), /ModuleRoot/);
  await writeFile(join(f.dir, 'main.js'), 'export default {};\n//# sourceMappingURL=../escape.map\n');
  await assert.rejects(invoke(f.config, '-DryRun'), /ModuleRoot/);
});
test('unknown migration and external API override fail before worker mutation', async () => {
  const f = await fixture('migration-failure', { migrations: [{ tag: 'known', new_classes: ['Known'] }] });
  await mock(() => ({ result: { default_environment: { script: { migration_tag: 'unexpected' } } } }), async (url, requests) => {
    await assert.rejects(invoke(f.config, `-ApiBase ${quote(url)} -AccountId account -ApiToken fixture-token`), /migration tag/);
    assert.ok(requests.every(x => x.method === 'GET'));
  });
  await assert.rejects(invoke(f.config, '-ApiBase https://example.invalid -AccountId account -ApiToken fixture-token'), /loopback/);
});
test('PowerShell Set-Location resolves explicit paths while config-relative defaults stay at config root', async () => {
  const f = await fixture('relative-inputs', { assets: { directory: './assets' } });
  await writeFile(join(f.dir, 'modules.json'), '["main.js", "data.json"]');
  await writeFile(join(f.dir, 'synthetic.json'), '{"SYNTHETIC":"relative-path-fixture"}');
  async function runAt(location, args) {
    const command = `try { [Environment]::CurrentDirectory = ${quote(root)}; Set-Location -LiteralPath ${quote(location)}; & ${quote(entry)} ${args} -AccountId '' -ApiToken '' -DryRun | ConvertTo-Json -Depth 100 -Compress } catch { [Console]::Error.WriteLine($_.Exception.GetBaseException().Message); exit 1 }`;
    const { stdout } = await runPowerShell(command);
    return JSON.parse(stdout.trim());
  }
  const defaults = await runAt(root, '-ConfigPath ./relative-inputs/wrangler.jsonc');
  assert.equal(defaults.AssetCount, 1); assert.deepEqual(defaults.Modules, ['main.js', 'data.json']);
  const explicit = await runAt(f.dir, '-ConfigPath ./wrangler.jsonc -Main ./main.js -ModuleRoot . -ModuleManifestPath ./modules.json -SecretsFile ./synthetic.json');
  assert.equal(explicit.AssetCount, 1); assert.deepEqual(explicit.SourceMaps, ['main.js.map']);
  assert.equal(explicit.Metadata.bindings.find(x => x.name === 'SYNTHETIC').text, '[redacted]');
});
test('explicit missing SecretsFile still fails in DryRun and with API token before HTTP', async () => {
  const f = await fixture('missing-secrets');
  const missing = join(f.dir, 'missing-secrets.json');
  await assert.rejects(invoke(f.config, `-DryRun -SecretsFile ${quote(missing)}`), /missing-secrets/);
  await assert.rejects(invoke(f.config, `-DryRun -ApiToken fixture-token -SecretsFile ${quote(missing)}`), /missing-secrets/);
  await mock(() => ({ status: 500 }), async (url, requests) => {
    await assert.rejects(invoke(f.config, `-ApiBase ${quote(url)} -AccountId account -ApiToken fixture-token -SecretsFile ${quote(missing)}`), /missing-secrets/);
    assert.equal(requests.length, 0);
  });
});
