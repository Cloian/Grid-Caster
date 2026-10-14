// 빌드 폴더만 공유한다. 프로젝트 소스/상위 경로는 제공하지 않는다.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { pipeline } from 'node:stream';

const root=fs.realpathSync(process.argv[2]||'Builds/MobileWeb');
const host=process.argv[3]||'127.0.0.1';
const port=Number(process.argv[4]||8765);
const mime={'.html':'text/html; charset=utf-8','.json':'application/json; charset=utf-8','.js':'application/javascript','.wasm':'application/wasm','.data':'application/octet-stream','.png':'image/png','.jpg':'image/jpeg','.css':'text/css','.mp4':'video/mp4'};
http.createServer((request,response)=>{
  if(!['GET','HEAD'].includes(request.method)){response.writeHead(405);response.end();return;}
  let relative;
  try{relative=decodeURIComponent(new URL(request.url,'http://local').pathname);}catch{response.writeHead(400);response.end();return;}
  if(relative==='/')relative='/index.html';
  let file=path.resolve(root,'.'+relative);
  if(!file.startsWith(root+path.sep)){response.writeHead(403);response.end();return;}
  if(!fs.existsSync(file)){
    if(fs.existsSync(file+'.br'))file+='.br';
    else if(fs.existsSync(file+'.gz'))file+='.gz';
  }
  let stat;
  try{if(!fs.realpathSync(file).startsWith(root+path.sep))throw new Error('outside root');stat=fs.statSync(file);if(!stat.isFile())throw new Error('not a file');}
  catch{response.writeHead(404);response.end();return;}
  const compressed=/\.(br|gz)$/.exec(file);
  const plain=compressed?file.slice(0,-compressed[0].length):file;
  response.setHeader('Content-Type',mime[path.extname(plain)]||'application/octet-stream');
  response.setHeader('Cache-Control','no-store');
  response.setHeader('X-Content-Type-Options','nosniff');
  // 같은 Wi-Fi의 HTTP Safari에서도 Brotli 빌드를 읽도록 서버에서 압축을 풀어 보낸다.
  // 브라우저와 Unity fallback의 이중 압축 해제를 피하기 위해 Content-Encoding은 붙이지 않는다.
  if(!compressed)response.setHeader('Content-Length',stat.size);
  response.writeHead(200);
  if(request.method==='HEAD'){response.end();return;}
  const source=fs.createReadStream(file);
  if(compressed)pipeline(source,compressed[1]==='br'?zlib.createBrotliDecompress():zlib.createGunzip(),response,error=>{if(error)response.destroy(error);});
  else pipeline(source,response,error=>{if(error)response.destroy(error);});
}).listen(port,host,()=>console.log(`Grid Caster playtest: http://${host}:${port}\nServing only ${root}`));
