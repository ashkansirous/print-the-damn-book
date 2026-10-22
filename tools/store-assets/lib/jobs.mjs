import path from 'node:path';
import { APP_ASSETS_DIR, STORE_OUT_DIR } from './paths.mjs';
import { MSIX_ASSETS, icoArtboard, storeArtboards } from './artboards.mjs';

/** What a render job produces. */
export const JobKind = Object.freeze({ Ico: 'ico', Msix: 'msix', Store: 'store' });

export const MSIX_SCALES = [100, 125, 150, 200, 400];
export const ICO_FILE = path.join(APP_ASSETS_DIR, 'app.ico');
export const ICO_SIZES = [
  { size: 256, source: 'icons/app-icon.svg' },
  { size: 64, source: 'icons/app-icon.svg' },
  { size: 48, source: 'icons/app-icon.svg' },
  { size: 32, source: 'icons/app-icon-sm.svg' },
  { size: 24, source: 'icons/app-icon-sm.svg' },
  { size: 16, source: 'icons/app-icon-sm.svg' },
];

/** MSIX rounds a scaled size to the nearest pixel, halves up (71 x 1.25 = 88.75 -> 89, 50 x 1.25 = 62.5 -> 63). */
export function scaledPixels(cssPixels, percent) {
  const exact = (cssPixels * percent) / 100;
  return Math.floor(exact + 0.5);
}

function msixJobs() {
  return MSIX_ASSETS.flatMap((asset) =>
    MSIX_SCALES.map((percent) => ({
      id: `${asset.name}.scale-${percent}`,
      file: path.join(APP_ASSETS_DIR, `${asset.name}.scale-${percent}.png`),
      html: asset.html(),
      cssW: asset.w,
      cssH: asset.h,
      outW: scaledPixels(asset.w, percent),
      outH: scaledPixels(asset.h, percent),
      kind: JobKind.Msix,
    })),
  );
}

function storeJobs() {
  return storeArtboards().map((art) => ({
    id: art.file.replace(/\.png$/, ''),
    file: path.join(STORE_OUT_DIR, art.file),
    html: art.html,
    cssW: art.w,
    cssH: art.h,
    outW: art.w * art.scale,
    outH: art.h * art.scale,
    kind: JobKind.Store,
  }));
}

function icoJobs() {
  return ICO_SIZES.map(({ size, source }) => ({
    id: `ico-${size}`,
    file: null,
    html: icoArtboard(size, source),
    cssW: size,
    cssH: size,
    outW: size,
    outH: size,
    kind: JobKind.Ico,
  }));
}

export function allJobs() {
  return [...icoJobs(), ...msixJobs(), ...storeJobs()];
}
