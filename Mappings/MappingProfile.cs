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
                .ForMember(d => d.TournamentType,
                    o => o.MapFrom(s => s.TournamentType.ToString()))
                .ForMember(d => d.Format,
                    o => o.MapFrom(s => s.Format.ToString()))
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s => s.SportName.ToString()))
                .ForMember(d => d.Status,
                    o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateTournamentDto, Tournament>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.TournamentType,
                    o => o.MapFrom(s =>
                        Enum.Parse<TournamentType>(s.TournamentType, true)))
                .ForMember(d => d.Format,
                    o => o.MapFrom(s =>
                        Enum.Parse<TournamentFormat>(s.Format, true)))
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s =>
                        Enum.Parse<SportName>(s.SportName, true)));

            CreateMap<UpdateTournamentDto, Tournament>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedBy, o => o.Ignore())
                .ForMember(d => d.TournamentParticipants, o => o.Ignore())
                .ForMember(d => d.NumberOfParticipants, o => o.Ignore())
                .ForMember(d => d.Settings, o => o.Ignore())
                .ForMember(d => d.Status, o => o.Ignore())
                .ForMember(d => d.TournamentType,
                    o => o.MapFrom(s =>
                        Enum.Parse<TournamentType>(s.TournamentType, true)))
                .ForMember(d => d.Format,
                    o => o.MapFrom(s =>
                        Enum.Parse<TournamentFormat>(s.Format, true)))
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s =>
                        Enum.Parse<SportName>(s.SportName, true)));


            // MATCH MAPPINGS
            CreateMap<Match, MatchDto>()
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s => s.SportName.ToString()))
                .ForMember(d => d.Status,
                    o => o.MapFrom(s => s.Status.ToString()));

            CreateMap<CreateMatchDto, Match>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s =>
                        Enum.Parse<SportName>(s.SportName, true)));

            CreateMap<UpdateMatchDto, Match>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.SportName,
                    o => o.MapFrom(s =>
                        Enum.Parse<SportName>(s.SportName, true)));

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