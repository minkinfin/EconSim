using TMPro;
using UnityEngine;

public class RoundDisplay : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public AuctionHouse auctionHouse;

    private void Awake()
    {
        scoreText = GetComponent<TextMeshProUGUI>();
    }
    void Start()
    {
        if (auctionHouse != null && scoreText != null)
        {
            UpdateScoreText();
        }
    }

    void Update()
    {
        if (auctionHouse != null && scoreText != null)
        {
            UpdateScoreText();
        }
    }

    void UpdateScoreText()
    {
        scoreText.text = auctionHouse.round.ToString();
    }
}
