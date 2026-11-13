namespace Beckend.Services.TournamentGenerators;

public static class SwissPairHelper
{
    public static string PairKey(string a, string b)
    {
        return string.CompareOrdinal(a, b) < 0
            ? $"{a}:{b}"
            : $"{b}:{a}";
    }
}