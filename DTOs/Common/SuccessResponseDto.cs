namespace Beckend.DTOs
{
    public class SuccessResponseDto<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; }
        public T Data { get; set; }
    }
}
