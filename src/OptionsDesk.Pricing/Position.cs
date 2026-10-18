namespace OptionsDesk.Pricing;

/// <summary>A position with average-cost P&amp;L. Quantity is signed: long &gt; 0, short &lt; 0.</summary>
public sealed class Position(string symbol, int multiplier = 1)
{
    public string Symbol { get; } = symbol;
    public int Multiplier { get; } = multiplier;
    public int Quantity { get; private set; }
    public decimal AveragePrice { get; private set; }
    public decimal RealizedPnl { get; private set; }

    public void Apply(Side side, int quantity, decimal price)
    {
        var signed = side.Sign() * quantity;
        if (Quantity == 0 || Quantity > 0 == signed > 0)
        {
            var newQuantity = Quantity + signed;
            AveragePrice = (AveragePrice * Math.Abs(Quantity) + price * Math.Abs(signed)) / Math.Abs(newQuantity);
            Quantity = newQuantity;
            return;
        }

        var direction = Quantity > 0 ? 1 : -1;
        var closed = Math.Min(Math.Abs(signed), Math.Abs(Quantity));
        RealizedPnl += direction * closed * (price - AveragePrice) * Multiplier;
        Quantity += signed;
        if (Quantity == 0)
            AveragePrice = 0;
        else if (Quantity > 0 != direction > 0)
            AveragePrice = price; // flipped through zero: the remainder opened at this price
    }

    public decimal UnrealizedPnl(decimal mark) => Quantity * (mark - AveragePrice) * Multiplier;

    public decimal MarketValue(decimal mark) => Quantity * mark * Multiplier;
}
