using Beckend.Models;

namespace Beckend.DataBase
{
    public class InMemoryDatabase
    {
        public static List<Team> Team { get; } = new();
        public static List<Statistic> Statistic { get; } = new();
        public static List<Tournament> Tournament { get; } = new();
        public static List <Sport> Sports { get; } = new();
        public static List <Match> Match { get; } = new();
        public static List<User> User { get; } = new();
    }
}
