namespace Beckend.DTOs
{
    public class CreateExtendedUserDto : CreateUserDto
    {
        public PlayerProfileDto PlayerProfile { get; set; }
        public DateTime? HireDate { get; set; }
    }
}
