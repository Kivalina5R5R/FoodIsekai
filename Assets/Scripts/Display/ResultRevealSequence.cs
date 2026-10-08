using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Reveals player earnings, then the team penalty, then the coin bonus and the big team total.
    // Finally each player row
    // fades in from slightly below while its own score counts up. The MVP badge arrives with the first row.
    // Rows return to their authored positions and full opacity when the panel is disabled.
    [AddComponentMenu("Food Isekai Z/Display/Result Reveal Sequence")]
    public sealed class ResultRevealSequence : MonoBehaviour
    {
        [SerializeField] private TMP_Text serviceText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private RectTransform[] rows = new RectTransform[0];
        [SerializeField] private TMP_Text[] rowScores = new TMP_Text[0];
        [SerializeField] private RectTransform firstRowBadge;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0f)] private float startDelay = 0.45f;
        [SerializeField, Min(0.05f)] private float serviceSeconds = 0.9f;
        [SerializeField, Min(0.05f)] private float bonusSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float totalSeconds = 0.9f;
        [SerializeField, Min(0f)] private float stepPause = 0.15f;
        [SerializeField, Min(0f)] private float rowInterval = 0.14f;
        [SerializeField, Min(0.05f)] private float rowFadeSeconds = 0.4f;
        [SerializeField, Min(0.05f)] private float rowCountSeconds = 0.7f;

        [Header("Row Entrance")]
        [SerializeField, Min(0f)] private float rowRise = 6f;

        private int serviceScore;
        private int penaltyScore;
        private int teamBonusScore;
        private int bonusScore;
        private int totalScore;
        private readonly List<int> playerScores = new List<int>();
        private bool playing;
        private float elapsed;
        private bool originsCaptured;
        private Vector2[] rowOrigins = new Vector2[0];
        private Vector2 badgeOrigin;
        private Graphic[][] rowGraphics = new Graphic[0][];
        private Graphic[] badgeGraphics = new Graphic[0];

        // Starts the sequence; the counting begins once this panel is active, so it can be called behind the cover.
        public void Play(int service, int bonus, int total, IReadOnlyList<int> scoresByRow,
            int penalty = 0, int teamBonus = 0)
        {
            SetScoreTargets(service, bonus, total, scoresByRow, penalty, teamBonus);
            elapsed = 0f;
            playing = true;
            if (isActiveAndEnabled) Apply();
        }

        // Refresh scores and their row order without restarting an in-progress or finished reveal.
        public void RefreshScores(int service, int bonus, int total, IReadOnlyList<int> scoresByRow,
            int penalty = 0, int teamBonus = 0)
        {
            SetScoreTargets(service, bonus, total, scoresByRow, penalty, teamBonus);
            if (isActiveAndEnabled) Apply();
        }

        private void SetScoreTargets(int service, int bonus, int total, IReadOnlyList<int> scoresByRow,
            int penalty, int teamBonus)
        {
            serviceScore = Mathf.Max(0, service);
            penaltyScore = Mathf.Max(0, penalty);
            teamBonusScore = Mathf.Max(0, teamBonus);
            bonusScore = Mathf.Max(0, bonus);
            totalScore = Mathf.Max(0, total);
            playerScores.Clear();
            if (scoresByRow != null) playerScores.AddRange(scoresByRow);
        }

        private void OnDisable()
        {
            if (!originsCaptured) return;
            for (int i = 0; i < rowOrigins.Length; i++)
            {
                if (rows[i] != null) rows[i].anchoredPosition = rowOrigins[i];
                SetAlpha(rowGraphics[i], 1f);
            }
            if (firstRowBadge != null) firstRowBadge.anchoredPosition = badgeOrigin;
            SetAlpha(badgeGraphics, 1f);
        }

        // The results stay up while gameplay is paused, so the sequence uses real time.
        private void LateUpdate()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            CaptureOrigins();
            float time = elapsed - startDelay;
            float penaltyStart = serviceSeconds + stepPause;
            float bonusStart = penaltyStart + (penaltyScore > 0 ? bonusSeconds + stepPause : 0f);
            float totalStart = bonusStart + bonusSeconds + stepPause;
            float rowsStart = totalStart + totalSeconds + stepPause;

            if (serviceText != null)
            {
                serviceText.text = ResultScoreText.Format(
                    Count(serviceScore, time / serviceSeconds),
                    Count(penaltyScore, (time - penaltyStart) / bonusSeconds),
                    Count(bonusScore, (time - bonusStart) / bonusSeconds),
                    Count(teamBonusScore, time / serviceSeconds));
            }
            if (totalText != null) totalText.text = Count(totalScore, (time - totalStart) / totalSeconds).ToString();

            int shown = 0;
            float finish = rowsStart;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null || !rows[i].gameObject.activeSelf) continue;
                float rowTime = time - rowsStart - shown * rowInterval;
                float fade = EaseOut(rowTime / rowFadeSeconds);
                Vector2 offset = Vector2.down * (1f - fade) * rowRise;
                rows[i].anchoredPosition = rowOrigins[i] + offset;
                SetAlpha(rowGraphics[i], fade);
                if (i < rowScores.Length && rowScores[i] != null)
                {
                    int score = i < playerScores.Count ? playerScores[i] : 0;
                    rowScores[i].text = Count(score, rowTime / rowCountSeconds).ToString();
                }
                if (i == 0 && firstRowBadge != null)
                {
                    firstRowBadge.anchoredPosition = badgeOrigin + offset;
                    SetAlpha(badgeGraphics, fade);
                }
                finish = rowsStart + shown * rowInterval + Mathf.Max(rowFadeSeconds, rowCountSeconds);
                shown++;
            }

            if (time >= finish && time >= totalStart + totalSeconds) playing = false;
        }

        private void CaptureOrigins()
        {
            if (originsCaptured) return;
            originsCaptured = true;
            rowOrigins = new Vector2[rows.Length];
            rowGraphics = new Graphic[rows.Length][];
            for (int i = 0; i < rows.Length; i++)
            {
                rowOrigins[i] = rows[i] != null ? rows[i].anchoredPosition : Vector2.zero;
                rowGraphics[i] = rows[i] != null ? rows[i].GetComponentsInChildren<Graphic>(true) : new Graphic[0];
            }
            if (firstRowBadge != null)
            {
                badgeOrigin = firstRowBadge.anchoredPosition;
                badgeGraphics = firstRowBadge.GetComponentsInChildren<Graphic>(true);
            }
        }

        private static int Count(int target, float progress) => Mathf.RoundToInt(target * EaseOut(progress));

        private static float EaseOut(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static void SetAlpha(Graphic[] graphics, float alpha)
        {
            foreach (Graphic graphic in graphics)
                if (graphic != null) graphic.canvasRenderer.SetAlpha(alpha);
        }
    }
}
