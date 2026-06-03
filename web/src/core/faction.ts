// The two opposing sides (plus a neutral state used while a ship is sinking) —
// a port of Unity `Core/Faction.cs`.

export enum Faction {
  Neutral = 0,
  British = 1,
  FrancoSpanish = 2,
}

export enum ControlMode {
  Human = 0,
  AI = 1,
}

export function enemyOf(faction: Faction): Faction {
  switch (faction) {
    case Faction.British:
      return Faction.FrancoSpanish;
    case Faction.FrancoSpanish:
      return Faction.British;
    default:
      return Faction.Neutral;
  }
}

/** Packs a 0..1 RGB triple into a 0xRRGGBB integer for PixiJS. */
function rgb(r: number, g: number, b: number): number {
  const ri = Math.round(r * 255);
  const gi = Math.round(g * 255);
  const bi = Math.round(b * 255);
  return (ri << 16) | (gi << 8) | bi;
}

/** Hull / banner colour used to render a faction's ships. */
export function bannerColor(faction: Faction): number {
  switch (faction) {
    case Faction.British:
      return rgb(0.85, 0.78, 0.62);
    case Faction.FrancoSpanish:
      return rgb(0.72, 0.36, 0.34);
    default:
      return 0x808080;
  }
}

/** Accent colour (sails / flags / UI) for a faction. */
export function accentColor(faction: Faction): number {
  switch (faction) {
    case Faction.British:
      return rgb(0.2, 0.45, 0.85);
    case Faction.FrancoSpanish:
      return rgb(0.9, 0.55, 0.2);
    default:
      return 0xffffff;
  }
}

/** Accent colour as a CSS hex string, for the DOM HUD. */
export function accentCss(faction: Faction): string {
  return "#" + accentColor(faction).toString(16).padStart(6, "0");
}

export function displayName(faction: Faction): string {
  switch (faction) {
    case Faction.British:
      return "British";
    case Faction.FrancoSpanish:
      return "Franco-Spanish";
    default:
      return "Neutral";
  }
}
