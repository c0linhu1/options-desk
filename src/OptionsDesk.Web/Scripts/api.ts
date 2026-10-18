// Typed calls to the desk's JSON endpoints. Writes carry the antiforgery token rendered into the page.

export type Side = "Buy" | "Sell";

export interface PricingResponse {
  symbol: string;
  spot: number;
  timeToExpiry: number;
  vol: number;
  price: number;
  delta: number;
  gamma: number;
  vega: number;
  theta: number;
  rho: number;
}

export interface TradeTicket {
  symbol: string;
  side: Side;
  quantity: number;
  price: number;
  account: string;
}

export interface BookingResult {
  booked: boolean;
  tradeId: number | null;
  symbol: string;
  settleDate: string | null;
  problems: string[];
}

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly problems: string[]) {
    super(problems.join("; "));
  }
}

function antiforgeryToken(): string {
  const input = document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
  return input?.value ?? "";
}

async function postJson<T>(url: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json", RequestVerificationToken: antiforgeryToken() },
    body: JSON.stringify(body),
    signal,
  });
  const payload = await res.json().catch(() => null);
  if (!res.ok) {
    const problems: string[] = payload?.problems ?? [payload?.detail ?? payload?.title ?? res.statusText];
    throw new ApiError(res.status, problems);
  }
  return payload as T;
}

export function impliedVol(symbol: string, price: number, signal?: AbortSignal): Promise<PricingResponse> {
  return postJson("/api/pricing", { symbol, price }, signal);
}

export function bookTrade(ticket: TradeTicket): Promise<BookingResult> {
  return postJson("/api/trades", ticket);
}
