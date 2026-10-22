import { losslessCompressPng } from '@napi-rs/image';
import { PNG } from 'pngjs';

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const IHDR_WIDTH_OFFSET = 16;
const IHDR_HEIGHT_OFFSET = 20;
const IHDR_COLOR_TYPE_OFFSET = 25;
const COLOR_TYPES_WITH_ALPHA = new Set([4, 6]);
export const COLOR_TYPE_RGB = 2;
export const COLOR_TYPE_RGBA = 6;
const OPAQUE = 255;
const ALPHA_CHANNEL = 3;

/** Reads width, height and colour type straight from the PNG header. */
export function readPngHeader(buffer) {
  if (!buffer.subarray(0, 8).equals(PNG_SIGNATURE)) throw new Error('Not a PNG file');
  return {
    width: buffer.readUInt32BE(IHDR_WIDTH_OFFSET),
    height: buffer.readUInt32BE(IHDR_HEIGHT_OFFSET),
    colorType: buffer[IHDR_COLOR_TYPE_OFFSET],
    hasAlpha: COLOR_TYPES_WITH_ALPHA.has(buffer[IHDR_COLOR_TYPE_OFFSET]),
  };
}

/** Throws when any pixel is not fully opaque, so dropping alpha cannot change the picture. */
function assertOpaque(png, label) {
  for (let i = ALPHA_CHANNEL; i < png.data.length; i += 4) {
    if (png.data[i] !== OPAQUE) throw new Error(`${label}: has a transparent pixel, cannot drop alpha`);
  }
}

/** Re-encodes an RGBA screenshot as an RGB PNG (colour type 2, no alpha channel). */
export function toOpaqueRgb(buffer, label) {
  const png = PNG.sync.read(buffer);
  assertOpaque(png, label);
  return PNG.sync.write(png, { colorType: COLOR_TYPE_RGB, inputColorType: 6, inputHasAlpha: true });
}

/** Losslessly recompresses an opaque RGB PNG; throws if the pixel layout or size would change. */
export async function optimiseRgbPng(buffer, label) {
  const before = readPngHeader(buffer);
  const smaller = await losslessCompressPng(buffer);
  const after = readPngHeader(smaller);
  const same = after.width === before.width && after.height === before.height && after.colorType === COLOR_TYPE_RGB;
  if (!same) throw new Error(`${label}: optimiser changed the image layout (colour type ${after.colorType})`);
  return smaller.length < buffer.length ? smaller : buffer;
}
