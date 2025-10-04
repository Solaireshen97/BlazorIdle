namespace BlazorIdle.Server.Domain.Characters;

public sealed class PrimaryAttributes
{
    public int Strength { get; init; } = 10;
    public int Agility { get; init; } = 10;
    public int Intellect { get; init; } = 10;
    public int Stamina { get; init; } = 10;

    public PrimaryAttributes() { }

    public PrimaryAttributes(int str, int agi, int intel, int sta)
    {
        Strength = str;
        Agility = agi;
        Intellect = intel;
        Stamina = sta;
    }
}