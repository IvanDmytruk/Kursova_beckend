using AutoMapper;
using Beckend.DTOs;
using Beckend.Models;
using Beckend.Enums;

namespace Beckend.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // USER MAPPINGS (без змін)
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
            CreateMap<CreateUserDto, User>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => Enum.Parse<UserRole>(src.Role)))
                .ForMember(dest => dest.Id, opt => opt.Ignore());
            CreateMap<UpdateUserDto, User>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => Enum.Parse<UserRole>(src.Role)))
                .ForMember(dest => dest.Id, opt => opt.Ignore());

            // ContactInfo mappings
            CreateMap<ContactInfo, ContactInfoDto>().ReverseMap();

            // TEAM MAPPINGS 
            CreateMap<Team, TeamDto>();
            CreateMap<CreateTeamDto, Team>();
            CreateMap<UpdateTeamDto, Team>();

            // TOURNAMENT MAPPINGS 
            CreateMap<Tournament, TournamentDto>()
                .ForMember(dest => dest.TournamentType, opt => opt.MapFrom(src => src.TournamentType.ToString()))
                .ForMember(dest => dest.Format, opt => opt.MapFrom(src => src.Format.ToString()));

            CreateMap<CreateTournamentDto, Tournament>()
                .ForMember(dest => dest.TournamentType, opt => opt.MapFrom(src => Enum.Parse<TournamentType>(src.TournamentType)))
                .ForMember(dest => dest.Format, opt => opt.MapFrom(src => Enum.Parse<TournamentFormat>(src.Format)));

            CreateMap<UpdateTournamentDto, Tournament>()
                .ForMember(dest => dest.TournamentType, opt => opt.MapFrom(src => Enum.Parse<TournamentType>(src.TournamentType)))
                .ForMember(dest => dest.Format, opt => opt.MapFrom(src => Enum.Parse<TournamentFormat>(src.Format)));

            // MATCH MAPPINGS 
            CreateMap<Match, MatchDto>();
            CreateMap<CreateMatchDto, Match>();
            CreateMap<UpdateMatchDto, Match>();

            // STATISTIC MAPPINGS 
            CreateMap<Statistic, StatisticDto>();
            CreateMap<CreateStatisticDto, Statistic>();
            CreateMap<UpdateStatisticDto, Statistic>();

            // SPORT MAPPINGS 
            CreateMap<Sport, SportDto>()
                .ForMember(dest => dest.SportName, opt => opt.MapFrom(src => src.SportName.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

            CreateMap<CreateSportDto, Sport>()
                .ForMember(dest => dest.SportName, opt => opt.MapFrom(src => Enum.Parse<SportName>(src.SportName)))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => Enum.Parse<TypeSport>(src.Type)));
        }
    }
}