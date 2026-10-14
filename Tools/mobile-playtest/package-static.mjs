// 검증한 Web 빌드를 정적 호스팅용으로 포장한다. Unity 소스/검증 로그는 복사하지 않는다.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import crypto from 'node:crypto';

const source = fs.realpathSync(process.argv[2] || 'Builds/MobileWeb');
if (!process.argv[3]) throw new Error('Usage: node package-static.mjs <build> <empty output directory>');
const output = path.resolve(process.argv[3]);
fs.mkdirSync(output, { recursive: true });
if (fs.readdirSync(output).length) throw new Error('Output must be empty; existing deployments are not overwritten.');
if (output === source || output.startsWith(source + path.sep)) throw new Error('Output must be outside the input build.');

const manifest = JSON.parse(fs.readFileSync(path.join(source, 'build-manifest.json'), 'utf8'));
const fields = ['loaderUrl', 'dataUrl', 'frameworkUrl', 'codeUrl'];
const assets = fields.map(field => {
    const relative = manifest[field];
    if (typeof relative !== 'string' || !/^Build\/[A-Za-z0-9._-]+$/.test(relative))
        throw new Error('Unexpected asset URL: ' + field);
    const plain = path.join(source, relative);
    const file = [plain, plain + '.br', plain + '.gz'].find(candidate => fs.existsSync(candidate));
    if (!file) throw new Error('Missing asset: ' + relative);
    if (!fs.realpathSync(file).startsWith(source + path.sep)) throw new Error('Asset outside build.');
    const original = fs.readFileSync(file);
    // Pages에서 .br 응답 헤더를 임의 설정할 수 없으므로 원래 바이트로 복구한다.
    // .unityweb는 Unity 로더의 fallback이 처리하므로 파일과 접미사를 그대로 유지한다.
    const bytes = file.endsWith('.br') ? zlib.brotliDecompressSync(original)
        : file.endsWith('.gz') ? zlib.gunzipSync(original) : original;
    return { field, name: path.basename(relative), bytes };
});
const digest = crypto.createHash('sha256');
for (const asset of assets) digest.update(asset.field).update(asset.bytes);
const buildId = digest.digest('hex').slice(0, 16);
const buildDirectory = path.join(output, 'Build', buildId);
fs.mkdirSync(buildDirectory, { recursive: true });
let totalBytes = 0;
for (const asset of assets) {
    fs.writeFileSync(path.join(buildDirectory, asset.name), asset.bytes);
    manifest[asset.field] = `Build/${buildId}/${asset.name}`;
    totalBytes += asset.bytes.length;
}
fs.copyFileSync(path.join(source, 'index.html'), path.join(output, 'index.html'));
fs.writeFileSync(path.join(output, 'build-manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
fs.writeFileSync(path.join(output, '.nojekyll'), '');
if (fs.existsSync(path.join(source, 'StreamingAssets')))
    throw new Error('StreamingAssets detected; review required before adding files to the public package.');
console.log(JSON.stringify({ output, buildId, totalBytes, files: assets.map(asset => manifest[asset.field]) }, null, 2));
