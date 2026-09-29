using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
namespace ElectronicRegisterAPI.Business.Services
{
    internal class ItsClassService : IItsClassService
    {
        private readonly IItsClassRepository _itsClassRepository;
        private readonly IStudyPathRepository _studyPathRepository;

        public ItsClassService(IItsClassRepository itsClassRepository, IStudyPathRepository studyPathRepository)
        {
            _itsClassRepository = itsClassRepository;
            _studyPathRepository = studyPathRepository;
        }
        
        public async Task EnsureBienniumStudyPathExistsAsync(Guid bienniumStudyPathId)
        {
            var bienniumStudyPath = await _itsClassRepository.GetByIdAsync(bienniumStudyPathId);
            if (bienniumStudyPath == null)
            {
                throw new ArgumentException("Il corso di studio non è stato trovato");
            }
        }

        public async Task EnsureValidItsClassNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Il nome della classe non è valido");
            }
            var existingClass = await _itsClassRepository.GetByNameAsync(name);
            if (existingClass != null)
            {
                throw new ArgumentException("La classe esiste già");
            }
        }

        public async Task<ItsClass> EnsureItsClassCanBeDeletedAsync(Guid itsClassId)
        {
            var itsClass = await _itsClassRepository.GetByIdAsync(itsClassId);
            if (itsClass == null)
            {
                throw new ArgumentException("La classe non è stata trovata");
            }
            return itsClass;
        }
    }
}
