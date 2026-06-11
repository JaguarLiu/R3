// One-off: generate square PWA icons from the (non-square) mascot sticker.
// Fits the whole mascot, centered, onto a white square with safe-zone padding
// so the maskable variant keeps content inside the central ~80%.
// Run: yarn node scripts/gen-pwa-icons.mjs
import sharp from 'sharp';

const SRC = 'public/favicon.png';
const WHITE = { r: 255, g: 255, b: 255, alpha: 1 };

// scale = fraction of the canvas the mascot occupies (rest is padding)
async function gen(size, scale, out) {
  const pad = Math.round((size * (1 - scale)) / 2);
  const inner = size - pad * 2;
  await sharp(SRC)
    .resize(inner, inner, { fit: 'contain', background: WHITE })
    .extend({ top: pad, bottom: pad, left: pad, right: pad, background: WHITE })
    .flatten({ background: WHITE })
    .png()
    .toFile(out);
  console.log(`wrote ${out} (${size}x${size}, mascot ${inner}px)`);
}

await gen(192, 0.92, 'public/pwa-192.png');
await gen(512, 0.92, 'public/pwa-512.png');
await gen(512, 0.78, 'public/pwa-512-maskable.png'); // extra padding for maskable safe zone
