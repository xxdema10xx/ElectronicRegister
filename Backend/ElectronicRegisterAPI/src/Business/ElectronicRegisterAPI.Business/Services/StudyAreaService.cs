using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services
{
    internal class StudyAreaService : IStudyAreaService
    {
        private readonly IStudyAreaRepository _studyAreaRepository;

        public StudyAreaService(IStudyAreaRepository studyAreaRepository)
        {
            _studyAreaRepository = studyAreaRepository;
        }

        public void EnsureValidStudyAreaName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Il nome dell'area di studio non può essere nullo o vuoto.");
            }
        }

        public void EnsureValidStudyAreaDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("La descrizione dell'area di studio non può essere nulla o vuota.");
            }
        }

        public async Task<StudyArea> EnsureStudyAreaExistsAsync(Guid id)
        {
            var studyArea = await _studyAreaRepository.GetByIdAsync(id);
            if (studyArea == null)
            {
                throw new KeyNotFoundException("L'area di studio specificata non esiste.");
            }
            return studyArea;
        }

        public async Task<StudyArea> EnsureStudyAreaCanBeDeletedAsync(Guid id)
        {
            var studyArea = await _studyAreaRepository.GetByIdAsync(id);
            if (studyArea == null)
            {
                throw new KeyNotFoundException("L'area di studio specificata non esiste.");
            }
            var hasStudyPaths = await _studyAreaRepository.HasStudyPathsAsync(id);
            if (hasStudyPaths)
            {
                throw new InvalidOperationException("L'area di studio non può essere eliminata perché è associata a percorsi di studio.");
            }
            return studyArea;
        }
    }
}