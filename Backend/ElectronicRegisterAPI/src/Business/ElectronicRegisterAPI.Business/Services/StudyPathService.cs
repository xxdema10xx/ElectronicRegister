using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services
{
    internal class StudyPathService : IStudyPathService
    {
        private readonly IStudyPathRepository _studyPathRepository;

        private readonly int _maxNameLength = 50;
        private readonly int _maxDescriptionLength = 500;

        public StudyPathService(IStudyPathRepository studyPathRepository)
        {
            _studyPathRepository = studyPathRepository;
        }

        public async Task<StudyPath> EnsureValidStudyPathIdAsync(Guid id)
        {
            var studyPath = await _studyPathRepository.GetByIdAsync(id);
            if (studyPath is null) throw new KeyNotFoundException("Il percorso di studio specificato non esiste.");
            return studyPath;
        }

        public void EnsureValidStudyPathName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Il nome del percorso di studio non può essere vuoto.");
            if (name.Length > _maxNameLength)
                throw new ArgumentException($"Il nome del percorso di studio non può superare i {_maxNameLength} caratteri.");
        }

        public void EnsureValidStudyPathDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("La descrizione del percorso di studio non può essere vuota.");
            if (description.Length > _maxDescriptionLength)
                throw new ArgumentException($"La descrizione del percorso di studio non può superare i {_maxDescriptionLength} caratteri.");
        }

        public async Task StudyPathAlreadyExistsByNameAsync(string name)
        {
            var studyPath = await _studyPathRepository.GetByNameAsync(name);
            if (studyPath is not null) throw new KeyNotFoundException("Il percorso di studio specificato esiste già.");
        }

        public async Task<StudyPath> EnsureStudyPathCanBeDeletedAsync(Guid id)
        {
            var studyPath = await _studyPathRepository.GetByIdAsync(id);
            if (studyPath is null) throw new KeyNotFoundException("Il percorso di studio specificato non esiste.");
            var hasStudents = await _studyPathRepository.HasClassesAsync(id);
            if (hasStudents) throw new InvalidOperationException("Il percorso di studio non può essere eliminato perché è associato a delle classi.");
            return studyPath;
        }
    }
}
