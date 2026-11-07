namespace Beckend.DTOs
{
    public class UpdateUserDto
    {
        public string Surname { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Role { get; set; }
        public ContactInfoDto ContactInfo { get; set; }
    }
}
