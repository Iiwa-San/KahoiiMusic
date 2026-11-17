// Exact port of KahoiiMusic's ColorExtractorService.cs
// Extracts dominant colors and builds a 4-stop dynamic gradient + accents matching the desktop app

export interface ThemePalette {
  max0: string;
  max1: string;
  max2: string;
  max3: string;
  accent: string;
  secondaryAccent: string;
  panelBg: string;
  panelBorder: string;
  btnGradient: string;
}

export const DEFAULT_THEME: ThemePalette = {
  max0: "#200C4B",
  max1: "#341487",
  max2: "#B90F8C",
  max3: "#FA0091",
  accent: "#FA0091",
  secondaryAccent: "#B032DC",
  panelBg: "rgba(45, 12, 85, 0.78)",
  panelBorder: "rgba(220, 20, 140, 0.35)",
  btnGradient: "linear-gradient(90deg, #990066 0%, #c20078 50%, #e6007e 100%)",
};

function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
  const rf = r / 255;
  const gf = g / 255;
  const bf = b / 255;

  const max = Math.max(rf, gf, bf);
  const min = Math.min(rf, gf, bf);
  const delta = max - min;

  let l = (max + min) / 2;
  let h = 0;
  let s = 0;

  if (delta < 0.0001) {
    return [0, 0, l];
  }

  s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

  if (Math.abs(max - rf) < 0.0001) {
    h = (gf - bf) / delta + (gf < bf ? 6 : 0);
  } else if (Math.abs(max - gf) < 0.0001) {
    h = (bf - rf) / delta + 2;
  } else {
    h = (rf - gf) / delta + 4;
  }

  h *= 60;
  if (h < 0) h += 360;

  return [h, s, l];
}

function colorChannelFromHue(p: number, q: number, t: number): number {
  let temp = t;
  if (temp < 0) temp += 1;
  if (temp > 1) temp -= 1;
  if (temp < 1 / 6) return p + (q - p) * 6 * temp;
  if (temp < 1 / 2) return q;
  if (temp < 2 / 3) return p + (q - p) * (2 / 3 - temp) * 6;
  return p;
}

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  const normH = ((h % 360) + 360) % 360;
  const normS = Math.max(0, Math.min(1, s));
  const normL = Math.max(0, Math.min(1, l));

  if (normS < 0.0001) {
    const val = Math.round(normL * 255);
    return [val, val, val];
  }

  const q = normL < 0.5 ? normL * (1 + normS) : normL + normS - normL * normS;
  const p = 2 * normL - q;

  const hk = normH / 360;
  const tr = hk + 1 / 3;
  const tg = hk;
  const tb = hk - 1 / 3;

  const r = Math.round(Math.max(0, Math.min(1, colorChannelFromHue(p, q, tr))) * 255);
  const g = Math.round(Math.max(0, Math.min(1, colorChannelFromHue(p, q, tg))) * 255);
  const b = Math.round(Math.max(0, Math.min(1, colorChannelFromHue(p, q, tb))) * 255);

  return [r, g, b];
}

function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (n: number) => n.toString(16).padStart(2, "0");
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

export function extractThemeFromImage(imgElement: HTMLImageElement): ThemePalette {
  try {
    const canvas = document.createElement("canvas");
    canvas.width = 24;
    canvas.height = 24;
    const ctx = canvas.getContext("2d", { willReadFrequently: true });
    if (!ctx) return DEFAULT_THEME;

    ctx.drawImage(imgElement, 0, 0, 24, 24);
    const imgData = ctx.getImageData(0, 0, 24, 24);
    const data = imgData.data;

    const binCount = 24;
    const binScores = new Float32Array(binCount);
    const binR = new Float32Array(binCount);
    const binG = new Float32Array(binCount);
    const binB = new Float32Array(binCount);
    let totalSat = 0;
    let validPixelCount = 0;

    for (let i = 0; i < data.length; i += 4) {
      const a = data[i + 3];
      if (a < 128) continue;

      const r = data[i];
      const g = data[i + 1];
      const b = data[i + 2];

      const [h, s, l] = rgbToHsl(r, g, b);
      validPixelCount++;
      totalSat += s;

      // Filter extreme black or white
      if (l < 0.08 || l > 0.92) continue;

      let lumBalance = 1.0 - Math.abs(l - 0.5) * 1.35;
      if (lumBalance < 0.1) lumBalance = 0.1;
      const score = s * s * lumBalance;

      let bin = Math.floor(h / (360 / binCount)) % binCount;
      if (bin < 0) bin = 0;

      binScores[bin] += score;
      binR[bin] += r * score;
      binG[bin] += g * score;
      binB[bin] += b * score;
    }

    if (validPixelCount === 0) return DEFAULT_THEME;

    const avgSat = totalSat / validPixelCount;
    let bestBin = -1;
    let bestScore = 0;
    for (let i = 0; i < binCount; i++) {
      if (binScores[i] > bestScore) {
        bestScore = binScores[i];
        bestBin = i;
      }
    }

    if (bestBin === -1 || bestScore < 0.5 || avgSat < 0.12) {
      // Sleek deep slate fallback
      return {
        max0: "#0e0f14",
        max1: "#181b24",
        max2: "#262b38",
        max3: "#3e4658",
        accent: "#8291af",
        secondaryAccent: "#5a6982",
        panelBg: "rgba(24, 27, 36, 0.8)",
        panelBorder: "rgba(130, 145, 175, 0.3)",
        btnGradient: "linear-gradient(90deg, #2d3444 0%, #4b566e 100%)",
      };
    }

    const winR = binR[bestBin] / bestScore;
    const winG = binG[bestBin] / bestScore;
    const winB = binB[bestBin] / bestScore;

    const [baseHue, baseSat] = rgbToHsl(
      Math.max(0, Math.min(255, Math.round(winR))),
      Math.max(0, Math.min(255, Math.round(winG))),
      Math.max(0, Math.min(255, Math.round(winB)))
    );

    const vividSat = Math.max(0.72, Math.min(baseSat * 1.15, 0.96));

    // Stop 0: Deep night tint
    const [r0, g0, b0] = hslToRgb(baseHue, 0.65, 0.13);
    // Stop 1: Rich mid tone (+6 deg)
    const [r1, g1, b1] = hslToRgb((baseHue + 6) % 360, vividSat * 0.85, 0.23);
    // Stop 2: Vibrant transition tone (+16 deg)
    const [r2, g2, b2] = hslToRgb((baseHue + 16) % 360, vividSat * 0.92, 0.38);
    // Stop 3: Radiant neon glow (+26 deg)
    const [r3, g3, b3] = hslToRgb((baseHue + 26) % 360, vividSat, 0.52);

    const max0 = rgbToHex(r0, g0, b0);
    const max1 = rgbToHex(r1, g1, b1);
    const max2 = rgbToHex(r2, g2, b2);
    const max3 = rgbToHex(r3, g3, b3);

    const accent = max3;
    const secondaryAccent = max2;
    const panelBg = `rgba(${r0 + 15}, ${g0 + 5}, ${b0 + 20}, 0.78)`;
    const panelBorder = `rgba(${r2}, ${g2}, ${b2}, 0.38)`;
    const btnGradient = `linear-gradient(90deg, ${rgbToHex(...hslToRgb((baseHue + 10) % 360, vividSat * 0.8, 0.28))} 0%, ${accent} 100%)`;

    return {
      max0,
      max1,
      max2,
      max3,
      accent,
      secondaryAccent,
      panelBg,
      panelBorder,
      btnGradient,
    };
  } catch (err) {
    console.warn("Theme extraction error:", err);
    return DEFAULT_THEME;
  }
}

export function loadAndExtractTheme(imageUrl: string): Promise<ThemePalette> {
  return new Promise((resolve) => {
    if (!imageUrl) {
      resolve(DEFAULT_THEME);
      return;
    }

    const img = new Image();
    img.crossOrigin = "anonymous";
    img.onload = () => {
      resolve(extractThemeFromImage(img));
    };
    img.onerror = () => {
      resolve(DEFAULT_THEME);
    };
    img.src = imageUrl;
  });
}

