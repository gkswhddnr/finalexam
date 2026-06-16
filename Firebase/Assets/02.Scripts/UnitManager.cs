using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 심화 1: 유닛 구매. UnitList(string,bool)를 코인으로 구매하여 Firebase에 저장한다.
// 이미 보유한 유닛은 다시 구매할 수 없다. ShopManager와 동일한 읽기-수정-쓰기 구조.
public class UnitManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-5294a-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;

    string userKey;
    int currentCoin;
    Dictionary<string, bool> unitList = new Dictionary<string, bool>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadUserData();
    }

    void LoadUserData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "유저 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());

                string unitListJson = snapshot.Child("UnitList").Value.ToString();
                unitList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitListJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "유닛 정보 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "보유 코인: " + currentCoin + " G";
    }

    bool IsOwned(string unitName)
    {
        return unitList.ContainsKey(unitName) && unitList[unitName];
    }

    public void OnClickBuyUnit2()
    {
        BuyUnit("Unit2", 150);
    }

    public void OnClickBuyUnit3()
    {
        BuyUnit("Unit3", 250);
    }

    public void OnClickBuyUnit4()
    {
        BuyUnit("Unit4", 350);
    }

    void BuyUnit(string unitName, int price)
    {
        // 이미 보유한 유닛은 재구매 불가
        if (IsOwned(unitName))
        {
            MessageText.text = unitName + " 은(는) 이미 보유한 유닛입니다.";
            return;
        }

        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        currentCoin -= price;
        unitList[unitName] = true;

        SaveUserData(unitName);
    }

    void SaveUserData(string boughtUnitName)
    {
        string unitListJson = JsonConvert.SerializeObject(unitList);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["UnitList"] = unitListJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "유닛 구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtUnitName + " 구매 완료";
                });
            });
    }
}
