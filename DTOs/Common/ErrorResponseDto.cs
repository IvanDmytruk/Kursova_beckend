namespace Beckend.DTOs
{
    // Для помилок
    public class ErrorResponseDto
    {
        public string Message { get; set; }
        public string Detail { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
