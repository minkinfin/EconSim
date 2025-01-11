public class AuctionRecord
{
    public int Produced { get; internal set; }
    public int Trades { get; internal set; }
    public int Consumed { get; internal set; }
    public int Bids { get; internal set; }
    public int Offers { get; internal set; }
    public int AvgOfferPrice { get; internal set; }
    public int AvgBidPrice { get; internal set; }
    public int AvgClearingPrice { get; internal set; }
    public int Stocks { get; internal set; }
    public int Capitals { get; internal set; }
    public int Profits { get; internal set; }
    public int Cashs { get; internal set; }
    public int Professions { get; internal set; }
}