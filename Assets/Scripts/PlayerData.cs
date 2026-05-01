using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public string id;
    public string nickname;
    public int score;
    public int rank;
}

[System.Serializable]
public class PlayerDataList
{
    public List<PlayerData> players;
}