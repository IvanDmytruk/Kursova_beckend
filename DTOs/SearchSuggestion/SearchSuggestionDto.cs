namespace Beckend.DTOs
{
    public class SearchSuggestionDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "team", "player", "tournament", "match"
        public string Subtitle { get; set; }
        public string Icon { get; set; }
        public int PopularityScore { get; set; }
    }

    public class SearchSuggestResponseDto
    {
        public string Query { get; set; }
        public List<SearchSuggestionDto> Suggestions { get; set; }
    }
}