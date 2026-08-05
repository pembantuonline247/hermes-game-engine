using UnityEngine;
using UnityEngine.UI;

namespace Hermes.SpaceDodger.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public Text scoreText;
        public Text highScoreText;
        public GameObject gameOverPanel;
        public Text finalScoreText;
        public Text newHighScoreText;
        public GameObject startPanel;

        private int currentScore = 0;
        private int highScore = 0;
        private const string HIGH_SCORE_KEY = "SpaceDodger_HighScore";

        private void Start()
        {
            highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            UpdateHighScoreUI();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
                GameManager.Instance.OnScoreChanged += OnScoreUpdate;
            }

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (startPanel != null) startPanel.SetActive(true);
            if (scoreText != null) scoreText.text = "";
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
                GameManager.Instance.OnScoreChanged -= OnScoreUpdate;
            }
        }

        private void OnGameStateChanged(GameManager.GameState state)
        {
            switch (state)
            {
                case GameManager.GameState.Menu:
                    if (startPanel != null) startPanel.SetActive(true);
                    if (gameOverPanel != null) gameOverPanel.SetActive(false);
                    if (scoreText != null) scoreText.text = "";
                    break;
                case GameManager.GameState.Playing:
                    currentScore = 0;
                    if (startPanel != null) startPanel.SetActive(false);
                    if (gameOverPanel != null) gameOverPanel.SetActive(false);
                    break;
                case GameManager.GameState.GameOver:
                    bool isNewHigh = currentScore > highScore;
                    if (isNewHigh)
                    {
                        highScore = currentScore;
                        PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
                        PlayerPrefs.Save();
                    }
                    if (gameOverPanel != null)
                    {
                        gameOverPanel.SetActive(true);
                        if (finalScoreText != null)
                            finalScoreText.text = $"Score: {currentScore}";
                        if (newHighScoreText != null)
                            newHighScoreText.gameObject.SetActive(isNewHigh);
                    }
                    break;
            }
        }

        private void OnScoreUpdate(int score)
        {
            currentScore = score;
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        private void UpdateHighScoreUI()
        {
            if (highScoreText != null)
                highScoreText.text = $"Best: {highScore}";
        }
    }
}