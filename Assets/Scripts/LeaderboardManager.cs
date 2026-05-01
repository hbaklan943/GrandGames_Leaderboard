using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

public class LeaderboardManager : MonoBehaviour
{
    enum ViewportLayoutMode { TopAnchored, CenteredOnMe, BottomAnchored }

    [Header("Prefabs")]
    public GameObject rowPrefab;

    [Header("Materials")]
    public Material normalMaterial;
    public Material meMaterial;

    [Header("Layout")]
    public float topAnchorY = 3.6f;
    public float bottomAnchorY = -3.7f;
    public float rowHeight = 1.6f;
    public int extraRowsAbove = 2;
    public int extraRowsBelow = 2;
    public float gapAnimationDuration = 0.8f;
    public float meInsertAnimationDuration = 0.5f;
    public float meDetachDuration = 0.8f;
    public Vector3 meDetachedScale = new Vector3(8.3f, 1.6f, 0.1f);
    public float meFlightDuration = 0.8f;
    public float meLandingDelay = 0.25f;
    public float scrollAnimationDuration = 3.5f;

    [Header("References")]
    public Transform updateButton;

    // Data
    private List<PlayerData> _sortedPlayers = new List<PlayerData>();
    private int _meIndex = 0;
    private int _oldMeIndex = 0;
    private int _oldMeRank = 0;
    private int _oldMeScore = 0;

    // Animation
    private bool _isAnimating = false;
    private bool _isMeFloating = false;
    private bool _hasAnimatedMeStats = false;
    private LeaderboardRow _meRow = null;

    // Viewport slots
    private List<LeaderboardRow> _visibleRows = new List<LeaderboardRow>();

    void Start()
    {
        LoadData();
        CreateViewportRows();
        BindRowsForCurrentViewport();
    }

    void LoadData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("players");
        if (jsonFile == null) { Debug.LogError("players.json not found in Resources!"); return; }

        _sortedPlayers = JsonUtility.FromJson<PlayerDataList>(jsonFile.text).players
            .OrderByDescending(p => p.score)
            .ToList();

        AssignRanks();

        _meIndex = _sortedPlayers.FindIndex(p => p.id == "me");
        _oldMeIndex = _meIndex;
        _oldMeRank = _sortedPlayers[_meIndex].rank;
        _oldMeScore = _sortedPlayers[_meIndex].score;

        Debug.Log($"Me index: {_meIndex} | Rank: {_sortedPlayers[_meIndex].rank}");
    }

    void AssignRanks()
    {
        for (int i = 0; i < _sortedPlayers.Count; i++)
            _sortedPlayers[i].rank = i + 1;
    }

    void CreateViewportRows()
    {
        for (int i = 0; i < GetViewportRowCount() + extraRowsAbove + extraRowsBelow; i++)
        {
            var row = Instantiate(rowPrefab).GetComponent<LeaderboardRow>();
            row.normalMaterial = normalMaterial;
            row.meMaterial = meMaterial;
            _visibleRows.Add(row);
        }
    }

    void BindRowsForCurrentViewport()
    {
        _meRow = null;
        int firstRankIndex = GetFirstVisibleRankIndex() - extraRowsAbove;
        ViewportLayoutMode layoutMode = GetViewportLayoutMode();

        for (int slotIndex = 0; slotIndex < _visibleRows.Count; slotIndex++)
        {
            LeaderboardRow row = _visibleRows[slotIndex];
            int rankIndex = firstRankIndex + slotIndex;
            row.transform.position = new Vector3(0f, GetSlotYPosition(rankIndex, layoutMode), 0f);

            if (rankIndex >= 0 && rankIndex < _sortedPlayers.Count)
            {
                PlayerData data = _sortedPlayers[rankIndex];
                bool isMe = data.id == "me";
                row.BindToRank(data, isMe, rankIndex);
                if (isMe) _meRow = row;
            }
            else
            {
                row.ClearSlot(rankIndex, false);
            }
        }
    }

    public void OnUpdateClicked()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _oldMeIndex = _meIndex;
        _oldMeRank = _sortedPlayers[_meIndex].rank;
        _oldMeScore = _sortedPlayers[_meIndex].score;
        _hasAnimatedMeStats = false;

        foreach (PlayerData player in _sortedPlayers)
            player.score = Random.Range(4500, 9501);

        _sortedPlayers = _sortedPlayers.OrderByDescending(p => p.score).ToList();
        AssignRanks();
        _meIndex = _sortedPlayers.FindIndex(p => p.id == "me");

        Debug.Log($"New me index: {_meIndex} | New rank: {_sortedPlayers[_meIndex].rank}");
        StartCoroutine(AnimateMe());
    }

    IEnumerator AnimateMe()
    {
        _meRow.transform.DOMove(new Vector3(0f, 0f, -1f), meFlightDuration).SetEase(Ease.InOutCubic);
        _meRow.transform.DOScale(meDetachedScale, meDetachDuration).SetEase(Ease.OutCubic);
        yield return new WaitForSeconds(meFlightDuration);

        Debug.Log("Me reached center");
        _isMeFloating = true;
        PrepareFloatingMeRow();
        RefreshVisibleRowsForCurrentSlots();
        yield return AnimateRowsContinuously();
        if (meLandingDelay > 0f)
            yield return new WaitForSeconds(meLandingDelay);
        yield return AnimateMeIntoFinalPosition();
        _isMeFloating = false;
        NormalizeRowBindingsAfterMeLands();

        _isAnimating = false;
    }

    void RefreshVisibleRowsForCurrentSlots()
    {
        foreach (LeaderboardRow row in _visibleRows)
        {
            if (row == null || row == _meRow) continue;

            int rankIndex = row.GetBoundRankIndex();
            if (rankIndex < 0 || rankIndex >= _sortedPlayers.Count) continue;

            int displayRankIndex = GetDisplayRankIndex(rankIndex);
            if (displayRankIndex < 0 || displayRankIndex >= _sortedPlayers.Count)
            {
                row.ClearSlot(rankIndex, false);
                continue;
            }

            PlayerData slotData = _sortedPlayers[displayRankIndex];
            if (_isMeFloating && slotData.id == "me")
            {
                row.ClearSlot(rankIndex, false);
                continue;
            }

            row.UpdateDisplay(slotData, slotData.id == "me");
        }
    }

    void PrepareFloatingMeRow()
    {
        if (_meRow == null || _meIndex < 0 || _meIndex >= _sortedPlayers.Count) return;
        _meRow.BindToRank(_sortedPlayers[_meIndex], true, _oldMeIndex);
        _meRow.SetRankAndScore(_oldMeRank, _oldMeScore);
    }

    IEnumerator AnimateRowsContinuously()
    {
        var scrollRows = _visibleRows.Where(r => r != null && r != _meRow).ToList();
        if (scrollRows.Count == 0) yield break;

        ViewportLayoutMode layoutMode = GetViewportLayoutMode();
        LeaderboardRow anchorRow = scrollRows.FirstOrDefault(r => r.GetData() != null) ?? scrollRows[0];
        float totalScrollY = GetSlotYPosition(anchorRow.GetBoundRankIndex(), layoutMode) - anchorRow.transform.position.y;

        if (Mathf.Approximately(totalScrollY, 0f)) yield break;

        float duration = Mathf.Max(0f, scrollAnimationDuration);
        if (Mathf.Approximately(duration, 0f))
        {
            UpdateRowsForScrollDelta(totalScrollY);
            TryAnimateMeStats(0f);
            yield break;
        }

        float previousScrollY = 0f;
        Tween meStatsTween = TryAnimateMeStats(duration);
        Tween scrollTween = DOVirtual.Float(0f, totalScrollY, duration, scrollY =>
        {
            float deltaY = scrollY - previousScrollY;
            previousScrollY = scrollY;
            UpdateRowsForScrollDelta(deltaY);
        }).SetEase(Ease.InOutCubic);

        yield return scrollTween.WaitForCompletion();
        if (meStatsTween != null && meStatsTween.IsActive())
            yield return meStatsTween.WaitForCompletion();
    }

    void UpdateRowsForScrollDelta(float deltaY)
    {
        var scrollRows = _visibleRows.Where(r => r != null && r != _meRow).ToList();
        if (scrollRows.Count == 0 || Mathf.Approximately(deltaY, 0f)) return;

        foreach (LeaderboardRow row in scrollRows)
            row.transform.position += new Vector3(0f, deltaY, 0f);

        RecycleRows(scrollRows, scrollingDown: deltaY > 0f);
    }

    IEnumerator AnimateMeIntoFinalPosition()
    {
        ViewportLayoutMode layoutMode = GetViewportLayoutMode();
        float meTargetY = GetSlotYPosition(_meIndex, layoutMode);
        Tween meStatsTween = TryAnimateMeStats(gapAnimationDuration);

        foreach (LeaderboardRow row in _visibleRows.Where(r => r != null && r != _meRow))
        {
            if (TryGetLandingTargetRankIndex(row, out int targetRankIndex))
                row.transform.DOMoveY(GetSlotYPosition(targetRankIndex, layoutMode), gapAnimationDuration).SetEase(Ease.OutCubic);
        }

        yield return new WaitForSeconds(gapAnimationDuration);
        if (meStatsTween != null && meStatsTween.IsActive())
            yield return meStatsTween.WaitForCompletion();

        _meRow.transform.DOMove(new Vector3(0f, meTargetY, 0f), meInsertAnimationDuration).SetEase(Ease.InOutCubic);
        _meRow.transform.DOScale(new Vector3(8f, 1.5f, 0.1f), meInsertAnimationDuration).SetEase(Ease.InOutCubic);
        yield return new WaitForSeconds(meInsertAnimationDuration);
    }

    Tween TryAnimateMeStats(float duration)
    {
        if (_hasAnimatedMeStats || _meRow == null || _meIndex < 0 || _meIndex >= _sortedPlayers.Count)
            return null;

        PlayerData meData = _sortedPlayers[_meIndex];
        int targetRank = meData.rank;
        int targetScore = meData.score;
        _hasAnimatedMeStats = true;

        if (Mathf.Approximately(duration, 0f))
        {
            _meRow.SetRankAndScore(targetRank, targetScore);
            return null;
        }

        return DOVirtual.Float(0f, 1f, duration, progress =>
        {
            _meRow.SetRankAndScore(
                Mathf.RoundToInt(Mathf.Lerp(_oldMeRank, targetRank, progress)),
                Mathf.RoundToInt(Mathf.Lerp(_oldMeScore, targetScore, progress)));
        })
        .SetEase(Ease.Linear)
        .OnComplete(() => _meRow.SetRankAndScore(targetRank, targetScore));
    }

    bool TryGetLandingTargetRankIndex(LeaderboardRow row, out int targetRankIndex)
    {
        targetRankIndex = -1;
        if (row?.GetData() == null) return false;

        int visualRankIndex = row.GetBoundRankIndex();

        if (_meIndex > _oldMeIndex && visualRankIndex > _oldMeIndex && visualRankIndex <= _meIndex)
        {
            targetRankIndex = visualRankIndex - 1;
            return true;
        }
        if (_meIndex < _oldMeIndex && visualRankIndex >= _meIndex && visualRankIndex < _oldMeIndex)
        {
            targetRankIndex = visualRankIndex + 1;
            return true;
        }

        return false;
    }

    void NormalizeRowBindingsAfterMeLands()
    {
        foreach (LeaderboardRow row in _visibleRows)
        {
            PlayerData data = row?.GetData();
            if (data == null) continue;
            row.BindToRank(data, data.id == "me", data.rank - 1);
        }
    }

    void BindRowToRankIndex(LeaderboardRow row, int rankIndex)
    {
        if (rankIndex < 0 || rankIndex >= _sortedPlayers.Count || _sortedPlayers[rankIndex].id == "me")
        {
            row.ClearSlot(rankIndex);
            return;
        }
        row.BindToRank(_sortedPlayers[rankIndex], false, rankIndex);
    }

    void RecycleRows(List<LeaderboardRow> scrollRows, bool scrollingDown)
    {
        float threshold = scrollingDown ? topAnchorY + extraRowsAbove * rowHeight : bottomAnchorY - extraRowsBelow * rowHeight;
        var crossed = scrollingDown
            ? scrollRows.Where(r => r.transform.position.y > threshold + 0.001f).OrderByDescending(r => r.transform.position.y).ToList()
            : scrollRows.Where(r => r.transform.position.y < threshold - 0.001f).OrderBy(r => r.transform.position.y).ToList();

        if (crossed.Count == 0) return;

        var remaining = scrollRows.Except(crossed).ToList();
        int nextRankIndex = scrollingDown
            ? scrollRows.Max(r => r.GetBoundRankIndex()) + 1
            : scrollRows.Min(r => r.GetBoundRankIndex()) - 1;
        float nextY = scrollingDown
            ? (remaining.Count == 0 ? threshold - scrollRows.Count * rowHeight : remaining.Min(r => r.transform.position.y) - rowHeight)
            : (remaining.Count == 0 ? threshold + scrollRows.Count * rowHeight : remaining.Max(r => r.transform.position.y) + rowHeight);

        foreach (LeaderboardRow row in crossed)
        {
            row.transform.position = new Vector3(0f, nextY, row.transform.position.z);
            BindViewportRankToRow(row, nextRankIndex);
            nextY += scrollingDown ? -rowHeight : rowHeight;
            nextRankIndex += scrollingDown ? 1 : -1;
        }
    }

    void BindViewportRankToRow(LeaderboardRow row, int viewportRankIndex)
    {
        int displayRankIndex = GetDisplayRankIndex(viewportRankIndex);

        if (displayRankIndex < 0 || displayRankIndex >= _sortedPlayers.Count)
        {
            row.ClearSlot(viewportRankIndex, false);
            return;
        }

        PlayerData data = _sortedPlayers[displayRankIndex];
        if (_isMeFloating && data.id == "me")
        {
            row.ClearSlot(viewportRankIndex, false);
            return;
        }

        row.BindToRank(data, data.id == "me", viewportRankIndex);
    }

    int GetDisplayRankIndex(int viewportRankIndex)
    {
        if (!_isMeFloating || _meIndex == _oldMeIndex) return viewportRankIndex;

        if (_meIndex > _oldMeIndex && viewportRankIndex > _oldMeIndex && viewportRankIndex <= _meIndex)
            return viewportRankIndex - 1;

        if (_meIndex < _oldMeIndex && viewportRankIndex >= _meIndex && viewportRankIndex < _oldMeIndex)
            return viewportRankIndex + 1;

        return viewportRankIndex;
    }

    int GetViewportRowCount() =>
        Mathf.Max(1, Mathf.FloorToInt((topAnchorY - bottomAnchorY) / rowHeight) + 1);

    int GetFirstVisibleRankIndex()
    {
        int half = GetViewportRowCount() / 2;
        int maxFirst = Mathf.Max(0, _sortedPlayers.Count - GetViewportRowCount());
        return Mathf.Clamp(_meIndex - half, 0, maxFirst);
    }

    ViewportLayoutMode GetViewportLayoutMode()
    {
        int playersBelowMe = (_sortedPlayers.Count - 1) - _meIndex;
        int maxAbove = Mathf.Max(0, Mathf.FloorToInt(topAnchorY / rowHeight));
        int maxBelow = Mathf.Max(0, Mathf.FloorToInt(Mathf.Abs(bottomAnchorY) / rowHeight));

        if (_meIndex <= maxAbove) return ViewportLayoutMode.TopAnchored;
        if (playersBelowMe <= maxBelow) return ViewportLayoutMode.BottomAnchored;
        return ViewportLayoutMode.CenteredOnMe;
    }

    float GetSlotYPosition(int rankIndex, ViewportLayoutMode layoutMode) => layoutMode switch
    {
        ViewportLayoutMode.TopAnchored    => topAnchorY - rankIndex * rowHeight,
        ViewportLayoutMode.BottomAnchored => bottomAnchorY + (_sortedPlayers.Count - 1 - rankIndex) * rowHeight,
        _                                 => (_meIndex - rankIndex) * rowHeight,
    };
}