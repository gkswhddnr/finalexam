using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-5294a-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text ManaCrystalCountText;
    [SerializeField] Text SpellScrollCountText;
    [SerializeField] Text PhoenixFeatherCountText;
    [SerializeField] Text MessageText;

    string userKey;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

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

        LoadInventory();
    }

    void LoadInventory()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 데이터가 없습니다.";
                    });
                    return;
                }

                string inventoryJson = snapshot.Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "인벤토리 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        ManaCrystalCountText.text = "마나 크리스탈: " + GetItemCount("ManaCrystal") + "개";
        SpellScrollCountText.text = "주문서: " + GetItemCount("SpellScroll") + "개";
        PhoenixFeatherCountText.text = "불사조 깃털: " + GetItemCount("PhoenixFeather") + "개";
    }

    int GetItemCount(string itemName)
    {
        if (inventory.ContainsKey(itemName))
        {
            return inventory[itemName];
        }

        return 0;
    }

    // 아이템별로 사용 메시지를 다르게 출력 (과제 필수 조건)
    string GetUseMessage(string itemName)
    {
        switch (itemName)
        {
            case "ManaCrystal": return "마나 크리스탈을 흡수해 마나가 가득 찼다!";
            case "SpellScroll": return "주문서를 펼쳐 마법을 시전했다!";
            case "PhoenixFeather": return "불사조의 깃털이 빛나며 부활의 가호를 받았다!";
            default: return itemName + " 사용 완료";
        }
    }

    public void OnClickUseManaCrystal()
    {
        UseItem("ManaCrystal");
    }

    public void OnClickUseSpellScroll()
    {
        UseItem("SpellScroll");
    }

    public void OnClickUsePhoenixFeather()
    {
        UseItem("PhoenixFeather");
    }

    void UseItem(string itemName)
    {
        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            MessageText.text = itemName + " 개수가 부족합니다.";
            return;
        }

        inventory[itemName]--;
        SaveInventory(itemName);
    }

    void SaveInventory(string usedItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .SetValueAsync(inventoryJson)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = GetUseMessage(usedItemName);
                });
            });
    }
}
