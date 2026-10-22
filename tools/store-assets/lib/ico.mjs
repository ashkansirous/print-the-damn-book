const ICONDIR_BYTES = 6;
const ICONDIRENTRY_BYTES = 16;
const ICO_TYPE_ICON = 1;
const BIT_DEPTH = 32;
const SIZE_256_CODE = 0;

/** Packs square PNG images into one .ico. Each entry is the PNG itself (supported since Windows Vista). */
export function packIco(images) {
  const header = Buffer.alloc(ICONDIR_BYTES);
  header.writeUInt16LE(ICO_TYPE_ICON, 2);
  header.writeUInt16LE(images.length, 4);
  const entries = [];
  let offset = ICONDIR_BYTES + ICONDIRENTRY_BYTES * images.length;
  for (const { size, png } of images) {
    entries.push(directoryEntry(size, png.length, offset));
    offset += png.length;
  }
  return Buffer.concat([header, ...entries, ...images.map((i) => i.png)]);
}

function directoryEntry(size, byteLength, offset) {
  const entry = Buffer.alloc(ICONDIRENTRY_BYTES);
  const code = size >= 256 ? SIZE_256_CODE : size;
  entry.writeUInt8(code, 0);
  entry.writeUInt8(code, 1);
  entry.writeUInt16LE(1, 4); // colour planes
  entry.writeUInt16LE(BIT_DEPTH, 6);
  entry.writeUInt32LE(byteLength, 8);
  entry.writeUInt32LE(offset, 12);
  return entry;
}

/** Entries stored in an .ico, read back from its directory: nominal size and the embedded PNG bytes. */
export function readIcoEntries(buffer) {
  const count = buffer.readUInt16LE(4);
  return Array.from({ length: count }, (_, index) => {
    const at = ICONDIR_BYTES + index * ICONDIRENTRY_BYTES;
    const code = buffer.readUInt8(at);
    const byteLength = buffer.readUInt32LE(at + 8);
    const offset = buffer.readUInt32LE(at + 12);
    return { size: code === SIZE_256_CODE ? 256 : code, png: buffer.subarray(offset, offset + byteLength) };
  });
}
