// Display formatting that matches the server-side Fmt helpers.

const integer = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

export function money(x: number): string {
  return (x < 0 ? "-$" : "$") + integer.format(Math.abs(x));
}

export function pct(x: number, decimals = 1): string {
  return `${(x * 100).toFixed(decimals)}%`;
}

export function greek(x: number): string {
  return x.toFixed(3);
}

/** OCC symbols are 21 characters; anything shorter is treated as a stock ticker. */
export function isOptionSymbol(symbol: string): boolean {
  return /^[A-Z0-9.]{1,6}\s*\d{6}[CP]\d{8}$/.test(symbol.trim().toUpperCase());
}

export const OPTION_MULTIPLIER = 100;
