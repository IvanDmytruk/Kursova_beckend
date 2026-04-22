namespace Beckend.DTOs
{
    public class ContactInfoDto
    {
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; }
        public string Surname { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Role { get; set; }
        public ContactInfoDto ContactInfo { get; set; }
    }

    public class PlayerProfileDto
    {
        public int? JerseyNumber { get; set; }
        public int? TransferCost { get; set; }
        public int? MaxCost { get; set; }
    }

    public class ExtendedUserDto : UserDto
    {
        public PlayerProfileDto PlayerProfile { get; set; }
        public DateTime? HireDate { get; set; }
    }
    public class CreateUserDto
    {
        public string Surname { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Role { get; set; }
        public ContactInfoDto ContactInfo { get; set; }
    }

    public class UpdateUserDto
    {
        public string Surname { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Role { get; set; }
        public ContactInfoDto ContactInfo { get; set; }
    }

    public class CreateExtendedUserDto : CreateUserDto
    {
        public PlayerProfileDto PlayerProfile { get; set; }
        public DateTime? HireDate { get; set; }
    }
}