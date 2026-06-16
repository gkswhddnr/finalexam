using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 심화 3: 거래소(경매장). 내 아이템을 가격을 붙여 판매 등록하고,
// 다른 유저가 등록한 매물을 구매한다. 구매 시 구매자 코인 차감 + 인벤 증가,
// 판매자 코인 증가, 매물 판매 완료(IsSold) 처리를 멀티 경로로 한번에 갱신한다.
public class AuctionManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-5294a-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;
    [SerializeField] InputField SellPriceInput;
    [SerializeField] Text AuctionListText;

    string userKey;
    string nickName;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    List<AuctionData> auctions = new List<AuctionData>();
    int currentIndex;

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");
        nickName = PlayerPrefs.GetString("UserNickName");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadMyData();
        LoadAuctions();
    }

    void LoadMyData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() => { MessageText.text = "내 정보 불러오기 실패"; });
                    return;
                }

                DataSnapshot snapshot = task.Result;
                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());
                string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                dispatcher.Enqueue(() => { RefreshUI(); });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "보유 코인: " + currentCoin + " G";
    }

    // ---------------- 판매 등록 ----------------

    public void OnClickSellManaCrystal() { SellItem("ManaCrystal"); }
    public void OnClickSellSpellScroll() { SellItem("SpellScroll"); }
    public void OnClickSellPhoenixFeather() { SellItem("PhoenixFeather"); }

    void SellItem(string itemName)
    {
        int price;
        if (!int.TryParse(SellPriceInput.text, out price) || price <= 0)
        {
            MessageText.text = "판매 가격을 1 이상 숫자로 입력하세요.";
            return;
        }

        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            MessageText.text = itemName + " 을(를) 보유하고 있지 않습니다.";
            return;
        }

        inventory[itemName]--;
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        DatabaseReference auctionRef = reference.Child("AuctionList").Push();
        string auctionKey = auctionRef.Key;

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["UserInfo/" + userKey + "/Inventory"] = inventoryJson;
        updateData["AuctionList/" + auctionKey + "/AuctionKey"] = auctionKey;
        updateData["AuctionList/" + auctionKey + "/SellerKey"] = userKey;
        updateData["AuctionList/" + auctionKey + "/SellerNickName"] = nickName;
        updateData["AuctionList/" + auctionKey + "/ItemName"] = itemName;
        updateData["AuctionList/" + auctionKey + "/Count"] = 1;
        updateData["AuctionList/" + auctionKey + "/Price"] = price;
        updateData["AuctionList/" + auctionKey + "/IsSold"] = false;

        reference.UpdateChildrenAsync(updateData).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                dispatcher.Enqueue(() => { MessageText.text = "판매 등록 실패"; });
                return;
            }

            dispatcher.Enqueue(() =>
            {
                RefreshUI();
                MessageText.text = itemName + " 판매 등록 완료 (가격 " + price + ")";
            });
            LoadAuctions();
        });
    }

    // ---------------- 매물 조회 ----------------

    public void OnClickRefreshAuctions() { LoadAuctions(); }

    void LoadAuctions()
    {
        reference
            .Child("AuctionList")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() => { MessageText.text = "매물 불러오기 실패"; });
                    return;
                }

                DataSnapshot snapshot = task.Result;
                List<AuctionData> loaded = new List<AuctionData>();

                if (snapshot.Value != null)
                {
                    foreach (DataSnapshot child in snapshot.Children)
                    {
                        bool isSold = bool.Parse(child.Child("IsSold").Value.ToString());
                        if (isSold)
                            continue;

                        AuctionData a = new AuctionData();
                        a.AuctionKey = child.Child("AuctionKey").Value.ToString();
                        a.SellerKey = child.Child("SellerKey").Value.ToString();
                        a.SellerNickName = child.Child("SellerNickName").Value.ToString();
                        a.ItemName = child.Child("ItemName").Value.ToString();
                        a.Count = int.Parse(child.Child("Count").Value.ToString());
                        a.Price = int.Parse(child.Child("Price").Value.ToString());
                        a.IsSold = isSold;
                        loaded.Add(a);
                    }
                }

                dispatcher.Enqueue(() =>
                {
                    auctions = loaded;
                    currentIndex = 0;
                    ShowCurrentAuction();
                });
            });
    }

    void ShowCurrentAuction()
    {
        if (auctions.Count == 0)
        {
            AuctionListText.text = "등록된 매물이 없습니다.";
            return;
        }

        AuctionData a = auctions[currentIndex];
        AuctionListText.text =
            "[" + (currentIndex + 1) + "/" + auctions.Count + "] "
            + a.ItemName + " - " + a.Price + "G\n판매자: " + a.SellerNickName;
    }

    public void OnClickNextAuction()
    {
        if (auctions.Count == 0)
            return;

        currentIndex = (currentIndex + 1) % auctions.Count;
        ShowCurrentAuction();
    }

    // ---------------- 구매 ----------------

    public void OnClickBuy()
    {
        if (auctions.Count == 0)
        {
            MessageText.text = "구매할 매물이 없습니다.";
            return;
        }

        AuctionData a = auctions[currentIndex];

        if (a.SellerKey == userKey)
        {
            MessageText.text = "본인이 등록한 매물은 구매할 수 없습니다.";
            return;
        }

        if (currentCoin < a.Price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        BuyAuction(a);
    }

    void BuyAuction(AuctionData a)
    {
        // 판매자 코인을 먼저 읽어서 증가시킨 뒤, 멀티 경로로 한번에 갱신한다.
        reference
            .Child("UserInfo")
            .Child(a.SellerKey)
            .Child("Coin")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result.Value == null)
                {
                    dispatcher.Enqueue(() => { MessageText.text = "판매자 정보 확인 실패"; });
                    return;
                }

                int sellerCoin = int.Parse(task.Result.Value.ToString());
                int sellerNewCoin = sellerCoin + a.Price;
                int buyerNewCoin = currentCoin - a.Price;

                // 구매자(나) 인벤토리에 아이템 추가
                if (inventory.ContainsKey(a.ItemName))
                    inventory[a.ItemName] += a.Count;
                else
                    inventory[a.ItemName] = a.Count;

                string buyerInventoryJson = JsonConvert.SerializeObject(inventory);

                Dictionary<string, object> updateData = new Dictionary<string, object>();
                updateData["UserInfo/" + userKey + "/Coin"] = buyerNewCoin;
                updateData["UserInfo/" + userKey + "/Inventory"] = buyerInventoryJson;
                updateData["UserInfo/" + a.SellerKey + "/Coin"] = sellerNewCoin;
                updateData["AuctionList/" + a.AuctionKey + "/IsSold"] = true;

                reference.UpdateChildrenAsync(updateData).ContinueWith(task2 =>
                {
                    if (task2.IsFaulted)
                    {
                        dispatcher.Enqueue(() => { MessageText.text = "구매 실패"; });
                        return;
                    }

                    currentCoin = buyerNewCoin;

                    dispatcher.Enqueue(() =>
                    {
                        RefreshUI();
                        MessageText.text = a.ItemName + " 구매 완료 (가격 " + a.Price + ")";
                    });
                    LoadAuctions();
                });
            });
    }
}
