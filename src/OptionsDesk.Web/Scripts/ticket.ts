// Trade ticket on the chain page: clicking a quote fills the ticket, the analytics panel shows
// implied vol and Greeks at the entered price, and submitting books through the API.
import { ApiError, bookTrade, impliedVol, type Side } from "./api.js";
import { defaultQuantity, greek, isOptionSymbol, money, OPTION_MULTIPLIER, pct } from "./format.js";

const form = document.querySelector<HTMLFormElement>("#ticket");
if (form) init(form);

function init(form: HTMLFormElement): void {
  const field = (name: string) => form.elements.namedItem(name) as HTMLInputElement;
  const symbol = field("symbol");
  const quantity = field("quantity");
  const price = field("price");
  const account = field("account");
  const analytics = form.querySelector<HTMLElement>("#ticket-analytics")!;
  const errors = form.querySelector<HTMLElement>("#ticket-errors")!;
  const success = form.querySelector<HTMLElement>("#ticket-success")!;
  const submit = form.querySelector<HTMLButtonElement>("#ticket-submit")!;
  const quantityUnit = form.querySelector<HTMLElement>("#ticket-quantity-unit")!;

  // The quantity's unit depends on the instrument: contracts (x100 shares) for options, shares for stock.
  // When the instrument type changes, the old number means something else, so reset it to that type's default.
  let ticketIsOption = true;
  function setInstrumentKind(isOption: boolean): void {
    quantityUnit.textContent = isOption ? "(contracts)" : "(shares)";
    if (isOption !== ticketIsOption) quantity.value = String(defaultQuantity(isOption));
    ticketIsOption = isOption;
  }

  const side = (): Side => (form.querySelector<HTMLInputElement>('input[name="side"]:checked')?.value as Side) ?? "Buy";
  const setSide = (s: Side) => {
    form.querySelector<HTMLInputElement>(`input[name="side"][value="${s}"]`)!.checked = true;
  };
  const show = (el: HTMLElement, lines: string[] | null) => {
    el.hidden = !lines?.length;
    el.innerHTML = "";
    for (const line of lines ?? []) {
      const div = document.createElement("div");
      div.textContent = line;
      el.append(div);
    }
  };
  const setAnalytic = (name: string, text: string) => {
    analytics.querySelector<HTMLElement>(`[data-field="${name}"]`)!.textContent = text;
  };

  let pending: AbortController | null = null;
  let timer: number | undefined;

  async function refreshAnalytics(): Promise<void> {
    pending?.abort();
    const sym = symbol.value.trim();
    const px = Number(price.value);
    const qty = Number(quantity.value);
    const multiplier = isOptionSymbol(sym) ? OPTION_MULTIPLIER : 1;
    if (!sym || !(px > 0)) {
      analytics.hidden = true;
      return;
    }
    setAnalytic("notional", money(qty * px * multiplier));
    if (!isOptionSymbol(sym)) {
      for (const name of ["vol", "delta", "vega", "theta"]) setAnalytic(name, name === "delta" ? "1.000" : "—");
      analytics.hidden = false;
      return;
    }
    pending = new AbortController();
    try {
      const q = await impliedVol(sym, px, pending.signal);
      setAnalytic("vol", pct(q.vol));
      setAnalytic("delta", greek(q.delta));
      setAnalytic("vega", money(q.vega * qty * multiplier));
      setAnalytic("theta", money(q.theta * qty * multiplier));
      analytics.hidden = false;
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return;
      for (const name of ["vol", "delta", "vega", "theta"]) setAnalytic(name, "—");
    }
  }

  function scheduleRefresh(): void {
    window.clearTimeout(timer);
    timer = window.setTimeout(refreshAnalytics, 250);
  }

  function fill(sym: string, s: Side, px: string): void {
    symbol.value = sym;
    setSide(s);
    price.value = px;
    setInstrumentKind(isOptionSymbol(sym));
    show(errors, null);
    show(success, null);
    void refreshAnalytics();
  }

  document.querySelectorAll<HTMLElement>("[data-quote]").forEach((cell) =>
    cell.addEventListener("click", () => fill(cell.dataset.symbol!, cell.dataset.side as Side, cell.dataset.price!)),
  );
  document.querySelectorAll<HTMLElement>("[data-stock]").forEach((button) =>
    button.addEventListener("click", () => fill(button.dataset.stock!, "Buy", button.dataset.price!)),
  );
  for (const input of [symbol, quantity, price]) input.addEventListener("input", scheduleRefresh);
  symbol.addEventListener("change", () => setInstrumentKind(isOptionSymbol(symbol.value)));

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    show(errors, null);
    show(success, null);
    submit.disabled = true;
    try {
      const result = await bookTrade({
        symbol: symbol.value.trim(),
        side: side(),
        quantity: Number(quantity.value),
        price: Number(price.value),
        account: account.value.trim(),
      });
      const unit = ticketIsOption ? "contracts" : "shares";
      show(success, [
        `Booked #${result.tradeId}: ${side()} ${quantity.value} ${unit} ${result.symbol}, settles ${result.settleDate}`,
      ]);
    } catch (e) {
      show(errors, e instanceof ApiError ? e.problems : [String(e)]);
    } finally {
      submit.disabled = false;
    }
  });
}
