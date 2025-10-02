namespace BlazorIdle.Server.Domain.Characters;

public class Character
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}