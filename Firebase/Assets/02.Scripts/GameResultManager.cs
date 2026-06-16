using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 심화 2: 게임 결과 저장. 게임 종료 시 보상 코인을 항상 지급하고,
// 이번 점수가 기존 최고 점수보다 높을 때만 Score를 갱신하여 Firebase에 저장한다.
public class GameResultManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-5294a-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text ScoreText;
    [SerializeField] Text MessageText;
    [SerializeField] InputField ScoreInput;

    [Header("Reward")]
    [SerializeField] int rewardCoin = 100;

    string userKey;
    int currentCoin;
    int bestScore;

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
                bestScore = int.Parse(snapshot.Child("Score").Value.ToString());

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "정보 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "보유 코인: " + currentCoin + " G";
        ScoreText.text = "최고 점수: " + bestScore;
    }

    // 게임 종료 버튼에 연결: 보상 코인 지급 + 최고 점수 갱신 시도
    public void OnClickGameOver()
    {
        int newScore;
        if (!int.TryParse(ScoreInput.text, out newScore))
        {
            MessageText.text = "이번 게임 점수를 숫자로 입력하세요.";
            return;
        }

        // 게임 종료 보상 코인은 항상 지급
        currentCoin += rewardCoin;

        // 기존 최고 점수보다 높을 때만 갱신
        bool isNewRecord = false;
        if (newScore > bestScore)
        {
            bestScore = newScore;
            isNewRecord = true;
        }

        SaveResult(isNewRecord);
    }

    void SaveResult(bool isNewRecord)
    {
        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["Score"] = bestScore;

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
                        MessageText.text = "결과 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    if (isNewRecord)
                    {
                        MessageText.text = "신기록 달성! 보상 코인 " + rewardCoin + " 지급";
                    }
                    else
                    {
                        MessageText.text = "최고 점수 유지. 보상 코인 " + rewardCoin + " 지급";
                    }
                });
            });
    }
}
