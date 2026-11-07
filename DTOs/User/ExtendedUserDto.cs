namespace Beckend.DTOs
{
    public class ExtendedUserDto : UserDto
    {
        public PlayerProfileDto PlayerProfile { get; set; }
        public DateTime? HireDate { get; set; }
    }
}
