using Beckend.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Runtime.CompilerServices;
namespace Beckend.Models
{
    public class ContactInfo
    {
        [BsonElement("Phone")]
        public string Phone { get; set; }
        [BsonElement("Email")]
        public string Email { get; set; }
        [BsonElement("Password")]
        public string Password { get; set; }
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
        [BsonElement("IsBanned")]
        public bool IsBanned { get; set; } = false;

        [BsonElement("BannedUntil")]
        public DateTime? BannedUntil { get; set; }
    }
    public class PlayerProfile
    {
        [BsonElement("JerseyNumber")]
        public int? JerseyNumber { get; set; }
        [BsonElement("TransferCost")]
        public int? TransferCost { get; set; }
        [BsonElement("MaxCost")]
        public int? MaxCost { get; set; } = int.MaxValue;
        [BsonElement("PopularityScore")]
        public int PopularityScore { get; set; } = 0;
        [BsonElement("TeamId")]
        public string? TeamId { get; set; }
    }
    public class ExtendedUser : User
    {
        [BsonElement("PlayerProfile")] 
        public PlayerProfile PlayerProfile { get; set; }
        [BsonElement("HireDate")] 
        public DateTime? HireDate { get; set; }
    }
    public class UserSession
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
    public class UserSavedItems
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        [BsonElement("UserId")]
        public string UserId { get; set; }
        [BsonElement("SavedMatches")]
        public List<string> SavedMatches { get; set; } = new List<string>();
        [BsonElement("SavedTeams")]
        public List<string> SavedTeams { get; set; } = new List<string>();
        [BsonElement("SavedTournaments")]
        public List<string> SavedTournaments { get; set; } = new List<string>();
        [BsonElement("SavedPlayers")]
        public List<string> SavedPlayers { get; set; } = new List<string>();
    }
    public class RoleRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("UserId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("RequestedRole")]
        public string RequestedRole { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Pending";

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UserEmail")]
        public string UserEmail { get; set; } = string.Empty;

        [BsonElement("UserName")]
        public string UserName { get; set; } = string.Empty;
    }
    public class UserBan
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("UserId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("BannedUntil")]
        public DateTime BannedUntil { get; set; }

        [BsonElement("Reason")]
        public string Reason { get; set; } = string.Empty;

        [BsonElement("BannedBy")]
        public string BannedBy { get; set; } = string.Empty;

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("IsActive")]
        public bool IsActive { get; set; } = true;

        public bool IsBannedActive()
        {
            if (!IsActive) return false;
            if (BannedUntil < DateTime.UtcNow)
            {
                IsActive = false; 
                return false;
            }
            return true;
        }
    }

}