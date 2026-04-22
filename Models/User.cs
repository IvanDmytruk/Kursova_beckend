using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Beckend.Enums;

namespace Beckend.Models
{
    public class ContactInfo
    {
        [BsonElement("Phone")]
        public string Phone { get; set; }

        [BsonElement("Email")]
        public string Email { get; set; }

        [BsonElement("Address")] 
        public string Address { get; set; }
    }

    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("Surname")]
        public string Surname { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; }

        [BsonElement("Age")]
        public int Age { get; set; }

        [BsonElement("Role")]
        public UserRole Role { get; set; }

        [BsonElement("ContactInfo")]
        public ContactInfo ContactInfo { get; set; }
        public User()
        {
            Id = ObjectId.GenerateNewId().ToString();
        }
    }

    public class PlayerProfile
    {
        [BsonElement("JerseyNumber")]
        public int? JerseyNumber { get; set; }

        [BsonElement("TransferCost")]
        public int? TransferCost { get; set; }

        [BsonElement("MaxCost")]
        public int? MaxCost { get; set; } = int.MaxValue;
    }

    public class ExtendedUser : User
    {
        [BsonElement("PlayerProfile")] 
        public PlayerProfile PlayerProfile { get; set; }

        [BsonElement("HireDate")] 
        public DateTime? HireDate { get; set; }
    }
}