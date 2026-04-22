// SportService.cs
using AutoMapper;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories;
using Beckend.DTOs;

namespace Beckend.Services
{
    public class SportService
    {
        private readonly SportRepository _sportRepository;
        private readonly IMapper _mapper;

        public SportService(SportRepository sportRepository, IMapper mapper)
        {
            _sportRepository = sportRepository;
            _mapper = mapper;
        }

        public async Task<SportDto> GetSportByNameAsync(SportName sportName)
        {
            var sport = await _sportRepository.GetSportByNameAsync(sportName);

            if (sport == null)
                throw new KeyNotFoundException($"Sport with name '{sportName}' not found");

            return _mapper.Map<SportDto>(sport);
        }

        public async Task<List<SportDto>> GetSportsByTypeAsync(TypeSport type)
        {
            var sports = await _sportRepository.GetSportsByTypeAsync(type);
            return _mapper.Map<List<SportDto>>(sports);
        }

        public async Task<List<SportDto>> GetActiveSportsAsync()
        {
            var sports = await _sportRepository.GetActiveSportsAsync();
            return _mapper.Map<List<SportDto>>(sports);
        }

        public async Task<List<SportDto>> SearchSportsByDescriptionAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<SportDto>();

            var sports = await _sportRepository.GetSportsByDescriptionSearchAsync(searchTerm);
            return _mapper.Map<List<SportDto>>(sports);
        }

        public async Task<bool> SportExistsAsync(SportName sportName)
        {
            var sport = await _sportRepository.GetSportByNameAsync(sportName);
            return sport != null;
        }

        public async Task<List<SportDto>> GetAllAsync()
        {
            var sports = await _sportRepository.GetAllAsync();
            return _mapper.Map<List<SportDto>>(sports);
        }

        public async Task<SportDto> GetByIdAsync(string id)
        {
            var sport = await _sportRepository.GetByIdAsync(id);
            return _mapper.Map<SportDto>(sport);
        }

        public async Task<SportDto> CreateAsync(CreateSportDto createDto)
        {
            var exists = await SportExistsAsync(Enum.Parse<SportName>(createDto.SportName));
            if (exists)
                throw new InvalidOperationException($"Sport with name '{createDto.SportName}' already exists");

            var sport = _mapper.Map<Sport>(createDto);
            await _sportRepository.CreateAsync(sport);
            return _mapper.Map<SportDto>(sport);
        }

        public async Task<SportDto> UpdateAsync(string id, UpdateSportDto updateDto)
        {
            var existing = await _sportRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Sport with id {id} not found");

            if (existing.SportName.ToString() != updateDto.SportName)
            {
                var exists = await SportExistsAsync(Enum.Parse<SportName>(updateDto.SportName));
                if (exists)
                    throw new InvalidOperationException($"Sport with name '{updateDto.SportName}' already exists");
            }

            _mapper.Map(updateDto, existing);
            existing.Id = id;
            await _sportRepository.UpdateAsync(id, existing);
            return _mapper.Map<SportDto>(existing);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _sportRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _sportRepository.DeleteAsync(id);
            return true;
        }

        public async Task<SportDto> ToggleSportActiveAsync(string id)
        {
            var sport = await _sportRepository.GetByIdAsync(id);
            if (sport == null)
                throw new KeyNotFoundException($"Sport with id {id} not found");

            sport.IsActive = !sport.IsActive;
            await _sportRepository.UpdateAsync(id, sport);
            return _mapper.Map<SportDto>(sport);
        }
    }
}