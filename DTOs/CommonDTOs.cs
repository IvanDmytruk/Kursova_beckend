namespace Beckend.DTOs
{
    // Для пагінації
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // Для помилок
    public class ErrorResponseDto
    {
        public string Message { get; set; }
        public string Detail { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Для успішних операцій
    public class SuccessResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; }
        public T Data { get; set; }
    }
}