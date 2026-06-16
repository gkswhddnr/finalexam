// 심화 3: 거래소(경매장) 매물 1건의 정보를 담는 데이터 클래스.
[System.Serializable]
public class AuctionData
{
    public string AuctionKey;
    public string SellerKey;
    public string SellerNickName;
    public string ItemName;
    public int Count;
    public int Price;
    public bool IsSold;

    public AuctionData()
    {
    }
}
