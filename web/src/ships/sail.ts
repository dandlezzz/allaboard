// How much canvas a ship is carrying. Four settings, from least to most sail by
// enum index (so the on-ship "+"/"-" buttons step naturally). Presentation order
// from most to least sail is: Full Sail → Reefed → Close-Reefed → Heave To.

export enum SailSetting {
  /** All canvas stowed: the ship makes no way (throttle 0 → stops). */
  HeaveTo = 0,
  /** Storm canvas: just enough to crawl. */
  CloseReefed = 1,
  /** Reduced canvas: a steady, moderate cruising speed. */
  Reefed = 2,
  /** All plain sail set for maximum speed. */
  FullSail = 3,
}

/** Fraction of the ship's top speed permitted by this sail plan. */
export function throttleFactor(setting: SailSetting): number {
  switch (setting) {
    case SailSetting.HeaveTo:
      return 0;
    case SailSetting.CloseReefed:
      return 0.3;
    case SailSetting.Reefed:
      return 0.6;
    case SailSetting.FullSail:
      return 1;
    default:
      return 0;
  }
}

/** Advances to the next sail setting (wraps): HeaveTo → … → FullSail → HeaveTo. */
export function nextSail(setting: SailSetting): SailSetting {
  return ((setting + 1) % 4) as SailSetting;
}

export function sailLabel(setting: SailSetting): string {
  switch (setting) {
    case SailSetting.HeaveTo:
      return "Heave To";
    case SailSetting.CloseReefed:
      return "Close-Reefed";
    case SailSetting.Reefed:
      return "Reefed";
    case SailSetting.FullSail:
      return "Full Sail";
    default:
      return "?";
  }
}
