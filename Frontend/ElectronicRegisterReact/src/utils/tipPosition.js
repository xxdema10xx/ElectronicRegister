// src/utils/tipPosition.js
// Posizione di un tooltip rispetto all'elemento che lo apre: centrato sull'elemento, sotto se c'è spazio,
// altrimenti sopra, e sempre dentro lo schermo.
const MARGIN = 12;
const GAP = 8;
const clamp = (n, min, max) => Math.max(min, Math.min(n, max));

export const tipWidth = (wanted, screenWidth) => Math.min(wanted, screenWidth - 2 * MARGIN);

// anchor: { x, y, width, height } in coordinate della finestra
export function tipPosition({ anchor, width, height, screenWidth, screenHeight }) {
  const left = clamp(anchor.x + anchor.width / 2 - width / 2, MARGIN, screenWidth - width - MARGIN);
  const below = anchor.y + anchor.height + GAP;
  const top = below + height <= screenHeight - MARGIN ? below : Math.max(MARGIN, anchor.y - GAP - height);
  return { left, top };
}
