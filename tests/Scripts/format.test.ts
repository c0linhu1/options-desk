import assert from "node:assert/strict";
import { test } from "node:test";
import { defaultQuantity, isOptionSymbol, quantityForKind, quantityUnit } from "../../src/OptionsDesk.Web/Scripts/format.ts";

test("a stock quantity doesn't carry over to an option (#5)", () => {
  assert.equal(quantityForKind("100", false, true), "10");
});

test("an option quantity doesn't carry over to stock", () => {
  assert.equal(quantityForKind("3", true, false), "100");
});

test("a quantity typed for the same type is kept", () => {
  assert.equal(quantityForKind("3", true, true), "3");
  assert.equal(quantityForKind("250", false, false), "250");
});

test("each type has its own default and unit", () => {
  assert.deepEqual([defaultQuantity(true), quantityUnit(true)], [10, "contracts"]);
  assert.deepEqual([defaultQuantity(false), quantityUnit(false)], [100, "shares"]);
});

test("OCC option symbols are told apart from stock tickers", () => {
  for (const s of ["AAPL261120C00200000", "aapl261120p00200000", "SPY   261218P00450000"]) assert.ok(isOptionSymbol(s), s);
  for (const s of ["AAPL", "BRK.B", "", "AAPL261120X00200000"]) assert.ok(!isOptionSymbol(s), s);
});
