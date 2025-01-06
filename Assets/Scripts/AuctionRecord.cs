public class AuctionRecord
{
    public int Produced { get; internal set; }
    public int Trades { get; internal set; }
    public int Consumed { get; internal set; }
    public int Bids { get; internal set; }
    public int Offers { get; internal set; }
    public float AvgOfferPrice { get; internal set; }
    public float AvgBidPrice { get; internal set; }
    public float AvgClearingPrice { get; internal set; }
    public float Stocks { get; internal set; }
    public float Capitals { get; internal set; }
    public float Profits { get; internal set; }
    public float Cashs { get; internal set; }
    public int Professions { get; internal set; }
}