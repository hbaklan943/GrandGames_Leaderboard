using UnityEngine;
using TMPro;
using DG.Tweening;

public class LeaderboardRow : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro rankText;
    public TextMeshPro nicknameText;
    public TextMeshPro scoreText;
    public Renderer backgroundRenderer;

    [Header("Materials")]
    public Material normalMaterial;
    public Material meMaterial;

    [Header("Animation")]
    public float updatePunchScale = 0.03f;
    public float updatePunchDuration = 0.2f;
    public int updatePunchVibrato = 3;
    public float updatePunchElasticity = 0.8f;

    private PlayerData _data;
    private bool _isMe;
    private int _boundRankIndex = -1;

    public void BindToRank(PlayerData data, bool isMe, int rankIndex)
    {
        _data = data;
        _isMe = isMe;
        _boundRankIndex = rankIndex;

        SetRankAndScore(data.rank, data.score);
        nicknameText.text = data.nickname;

        backgroundRenderer.material = isMe ? meMaterial : normalMaterial;
        SetContentVisible(true);
    }

    public void SetRankAndScore(int rank, int score)
    {
        rankText.text = "#" + rank.ToString();
        scoreText.text = score.ToString();
    }

    public void ClearSlot(int rankIndex, bool showPlaceholder = true)
    {
        _data = null;
        _isMe = false;
        _boundRankIndex = rankIndex;

        rankText.text = string.Empty;
        nicknameText.text = string.Empty;
        scoreText.text = string.Empty;

        backgroundRenderer.material = normalMaterial;
        backgroundRenderer.enabled = showPlaceholder;
        SetTextVisible(false);
    }

    // Instantly swap the row to another player's data while keeping the row in place.
    public void UpdateDisplay(PlayerData data, bool isMe)
    {
        BindToRank(data, isMe, _boundRankIndex);

        // Small punch scale animation for feedback
        transform.DOPunchScale(
            Vector3.one * updatePunchScale,
            updatePunchDuration,
            updatePunchVibrato,
            updatePunchElasticity);
    }

    // Animate "me" row flying to new position
    public void AnimateToPosition(Vector3 targetPosition, float duration)
    {
        transform.DOMove(targetPosition, duration)
            .SetEase(Ease.InOutCubic);
    }

    void SetContentVisible(bool isVisible)
    {
        SetTextVisible(isVisible);
        backgroundRenderer.enabled = isVisible;
    }

    void SetTextVisible(bool isVisible)
    {
        rankText.gameObject.SetActive(isVisible);
        nicknameText.gameObject.SetActive(isVisible);
        scoreText.gameObject.SetActive(isVisible);
    }

    public PlayerData GetData() => _data;
    public bool IsMe() => _isMe;
    public int GetBoundRankIndex() => _boundRankIndex;
}
