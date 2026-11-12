using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// HUD for mini-games: score, lives and a timer. Game scripts (or buttons) call AddScore, LoseLife,
    /// StartGame, StopTimer. When lives or time run out it shows the game-over group and can send an
    /// event to your game script. Local by default (each player's own run).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoGameHud : UdonSharpBehaviour
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI timerText;
        public GameObject gameOverGroup;
        public TextMeshProUGUI gameOverText;

        public int startLives = 3;
        [Tooltip("Seconds per round; 0 = count up instead.")]
        public float roundSeconds = 60f;
        public int pointsPerScore = 1;
        [Tooltip("Optional: your game script gets this event on game over.")]
        public UdonBehaviour gameOverTarget;
        public string gameOverEvent = "OnGameOver";
        [Tooltip("Optional: saves the best score between visits.")]
        public NekoScoreboard scoreboard;

        private int score;
        private int lives;
        private float time;
        private bool running;

        private void Start()
        {
            lives = startLives;
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            Draw();
        }

        public void StartGame()
        {
            score = 0;
            lives = startLives;
            time = roundSeconds > 0 ? roundSeconds : 0f;
            running = true;
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            Draw();
        }

        public void AddScore()
        {
            if (!running) return;
            score += pointsPerScore;
            Draw();
        }

        public void LoseLife()
        {
            if (!running) return;
            lives--;
            Draw();
            if (lives <= 0) GameOver("Out of lives");
        }

        public void StopTimer() => running = false;

        private void Update()
        {
            if (!running) return;
            if (roundSeconds > 0)
            {
                time -= Time.deltaTime;
                if (time <= 0f) { time = 0f; Draw(); GameOver("Time's up"); return; }
            }
            else time += Time.deltaTime;
            if (timerText != null) timerText.text = Mathf.CeilToInt(time).ToString();
        }

        private void GameOver(string why)
        {
            running = false;
            if (gameOverGroup != null) gameOverGroup.SetActive(true);
            if (gameOverText != null) gameOverText.text = why + "\nScore: " + score;
            if (scoreboard != null) { scoreboard.points = score; scoreboard.AddPoints(); }
            if (gameOverTarget != null && !string.IsNullOrEmpty(gameOverEvent)) gameOverTarget.SendCustomEvent(gameOverEvent);
        }

        private void Draw()
        {
            if (scoreText != null) scoreText.text = "Score " + score;
            if (livesText != null) livesText.text = lives > 0 && lives <= 10 ? new string('♥', lives) : "Lives " + lives;
            if (timerText != null) timerText.text = Mathf.CeilToInt(time).ToString();
        }
    }
}
