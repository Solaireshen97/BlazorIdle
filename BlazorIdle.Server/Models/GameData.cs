namespace BlazorIdle.Server.Models;

public class GameData
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
    public DateTime LastUpdated { get; set; }
}
