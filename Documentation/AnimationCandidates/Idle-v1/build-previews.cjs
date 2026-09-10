// Technical sprite-sheet extraction, registration and animation encoding.
// Artwork and all eight poses come from the generated source sheets.
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = __dirname;
const SIZE = 384;
const BASELINE = 316;
const BG = { r: 230, g: 240, b: 250, alpha: 1 };
const DELAYS = [600, 230, 230, 100, 160, 100, 260, 320];

async function bounds(input) {
  const { data, info } = await sharp(input).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  let x0 = info.width, y0 = info.height, x1 = -1, y1 = -1;
  const dark = (x,y) => {
    const p = (y * info.width + x) * 3;
    return Math.min(data[p], data[p+1], data[p+2]) < 125;
  };
  for (let y=0; y<info.height; y++) for (let x=0; x<info.width; x++) if (dark(x,y)) {
    x0 = Math.min(x0,x); x1 = Math.max(x1,x); y0 = Math.min(y0,y); y1 = Math.max(y1,y);
  }
  assert(x1>x0 && y1>y0, 'No sprite silhouette detected');
  let footL=info.width, footR=-1;
  for (let y=Math.max(y0,y1-10); y<=y1; y++) for (let x=x0; x<=x1; x++) if (dark(x,y)) {
    footL=Math.min(footL,x); footR=Math.max(footR,x);
  }
  return {x0,y0,x1,y1,footX:(footL+footR)/2,width:info.width,height:info.height};
}

async function animate(frames, output, delays=DELAYS) {
  const raw = await Promise.all(frames.map(f=>sharp(f).ensureAlpha().raw().toBuffer()));
  await sharp(Buffer.concat(raw), {raw:{width:SIZE,height:SIZE*frames.length,channels:4,pageHeight:SIZE}})
    .gif({loop:0,delay:delays,effort:7,dither:0,interFrameMaxError:0,colours:256}).toFile(output);
  const meta = await sharp(output,{animated:true}).metadata();
  assert.equal(meta.pages, frames.length, 'Frame count changed');
  assert.equal(meta.pageHeight,SIZE);
  assert.equal(meta.loop,0);
  assert.deepEqual(meta.delay,delays);
  return {file:path.basename(output),width:meta.width,height:meta.pageHeight,frames:meta.pages,loop:meta.loop,delays:meta.delay};
}

async function main() {
  const manifest = JSON.parse(await fs.readFile(path.join(root,'prompts.json'),'utf8'));
  const report = {created:new Date().toISOString(), mode:manifest.mode, frameSize:SIZE, baseline:BASELINE, totalLoopMs:DELAYS.reduce((a,b)=>a+b,0), candidates:[]};
  for (const item of manifest.candidates) {
    const dir=path.join(root,item.key);
    await fs.mkdir(dir,{recursive:true});
    const source=path.join(dir,'source-sheet.png');
    try { await fs.access(source); } catch { await fs.copyFile(item.source,source); }
    const meta=await sharp(source).metadata();
    const tiles=[];
    for (let i=0;i<8;i++) {
      const col=i%4, row=Math.floor(i/4);
      const left=Math.round(col*meta.width/4), top=Math.round(row*meta.height/2);
      const width=Math.round((col+1)*meta.width/4)-left, height=Math.round((row+1)*meta.height/2)-top;
      const png=await sharp(source).extract({left,top,width,height}).png().toBuffer();
      tiles.push({png,bbox:await bounds(png)});
    }
    // One shared scale preserves the relative size changes of the generated poses.
    const maxHeight=Math.max(...tiles.map(t=>t.bbox.y1-t.bbox.y0+1));
    const maxWidth=Math.max(...tiles.map(t=>t.bbox.x1-t.bbox.x0+1));
    const scale=Math.min(246/maxHeight,276/maxWidth);
    const frames=[];
    for (let i=0;i<tiles.length;i++) {
      const {png,bbox:b}=tiles[i];
      const width=Math.round(b.width*scale), height=Math.round(b.height*scale);
      const resized=await sharp(png).resize(width,height,{kernel:'nearest'}).png().toBuffer();
      const left=Math.round(SIZE/2-b.footX*scale);
      const top=Math.round(BASELINE-b.y1*scale);
      assert(left>=0 && top>=0 && left+width<=SIZE && top+height<=SIZE, item.key+' exceeds preview canvas');
      const frame=await sharp({create:{width:SIZE,height:SIZE,channels:4,background:BG}})
        .composite([{input:resized,left,top}]).png().toBuffer();
      frames.push(frame);
      await fs.writeFile(path.join(dir,`idle-${String(i).padStart(2,'0')}.png`),frame);
    }
    const animation=await animate(frames,path.join(root,item.key+'.gif'));
    const strip=await sharp({create:{width:SIZE*4,height:SIZE*2,channels:4,background:BG}})
      .composite(frames.map((input,i)=>({input,left:(i%4)*SIZE,top:Math.floor(i/4)*SIZE}))).png().toBuffer();
    await fs.writeFile(path.join(dir,'aligned-sheet.png'),strip);
    report.candidates.push({...item,prompt:undefined,source:undefined,scale,animation,bounds:tiles.map(t=>t.bbox)});
    process.stdout.write(JSON.stringify({key:item.key,...animation})+'\n');
  }
  const penguinFrames=[];
  await fs.mkdir(path.join(root,'penguin-reference'),{recursive:true});
  for (let i=0;i<8;i++) {
    const input=path.resolve(root,'../../../Assets/Cozy/Resources/Cozy',`Idle${i}.png`);
    const png=await sharp(input).resize(SIZE,SIZE,{kernel:'nearest'}).png().toBuffer();
    penguinFrames.push(png);
    await fs.writeFile(path.join(root,'penguin-reference',`idle-${String(i).padStart(2,'0')}.png`),png);
  }
  report.reference=await animate(penguinFrames,path.join(root,'penguin-reference.gif'));
  await fs.writeFile(path.join(root,'verification.json'),JSON.stringify(report,null,2));
  process.stdout.write('Validated six 8-frame looping candidates and original penguin reference.\n');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
