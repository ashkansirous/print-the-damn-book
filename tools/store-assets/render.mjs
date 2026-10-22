import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';
import { createCapturer } from './lib/capture.mjs';
import { packIco } from './lib/ico.mjs';
import { ICO_FILE, JobKind, allJobs } from './lib/jobs.mjs';
import { APP_ASSETS_DIR, STORE_OUT_DIR } from './lib/paths.mjs';
import { optimiseRgbPng, toOpaqueRgb } from './lib/png.mjs';
import { stageWorkDir } from './lib/staging.mjs';
import { verifyOutputs } from './lib/verify.mjs';

async function renderJob(capturer, job) {
  const png = await capturer.capture(job);
  if (job.kind === JobKind.Ico) return { size: job.outW, png };
  if (job.kind === JobKind.Unplated) {
    writeFileSync(job.file, png);
    return null;
  }
  writeFileSync(job.file, await optimiseRgbPng(toOpaqueRgb(png, job.id), job.id));
  return null;
}

async function renderAll(jobs) {
  const browser = await chromium.launch();
  const capturer = createCapturer(browser);
  const icoImages = [];
  try {
    for (const job of jobs) {
      const icoImage = await renderJob(capturer, job);
      if (icoImage) icoImages.push(icoImage);
      console.log(`rendered ${job.id} ${job.outW}x${job.outH}`);
    }
  } finally {
    await capturer.close();
    await browser.close();
  }
  writeFileSync(ICO_FILE, packIco(icoImages));
  return icoImages;
}

async function main() {
  mkdirSync(STORE_OUT_DIR, { recursive: true });
  mkdirSync(APP_ASSETS_DIR, { recursive: true });
  const jobs = allJobs();
  stageWorkDir(jobs);
  const icoImages = await renderAll(jobs);
  const problems = verifyOutputs(jobs, icoImages);
  if (problems.length) {
    console.error(`Verification failed:\n${problems.join('\n')}`);
    process.exit(1);
  }
  const pngCount = jobs.filter((job) => job.file).length;
  console.log(`OK: ${pngCount} PNGs and ${path.relative(process.cwd(), ICO_FILE)} verified`);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
