using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Business.Services
{
    internal class StudyPathService : IStudyPathService
    {
        private readonly IStudyPathRepository _studyPathRepository;

        public StudyPathService(IStudyPathRepository studyPathRepository)
        {
            _studyPathRepository = studyPathRepository;
        }

        public void EnsureValidStudyPathName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Il nome del percorso di studio non può essere vuoto.");
            if (name.Length > 100)
                throw new ArgumentException("Il nome del percorso di studio non può superare i 100 caratteri.");
        }

        public void EnsureValidStudyPathDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("La descrizione del percorso di studio non può essere vuota.");
            if (description.Length > 500)
                throw new ArgumentException("La descrizione del percorso di studio non può superare i 500 caratteri.");
        }

        public async Task EnsureStudyPathExistsAsync(Guid id)
        {
            var studyPath = await _studyPathRepository.GetByIdAsync(id);
            if (studyPath == null) throw new KeyNotFoundException("Il percorso di studio specificato non esiste.");
        }
    }
}
