namespace Beckend.Services.Search;

public interface ISearchService
{
    Task<object> GetSuggestionsAsync(
        string query,
        int limit = 10);

    Task IncrementPopularityAsync(
        string id,
        string type);
}
