using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Business.Services
{
    internal class BienniumService : IBienniumService
    {
        private readonly IBienniumRepository _bienniumRepository;
        private readonly int minYear = 2010;

        public BienniumService(IBienniumRepository bienniumRepository)
        {
            _bienniumRepository = bienniumRepository;
        }

        public void EnsureValidBienniumValue(int startYear, int endYear)
        {
            if (startYear < minYear || endYear < minYear || startYear >= endYear)
            {
                throw new ArgumentException($"L'anno di inizio e di fine devono essere maggiori o uguali a {minYear}.");
            }
            if (endYear - startYear != 2)
            {
                throw new ArgumentException($"L'anno di fine deve essere esattamente due anni dopo {startYear}.");
            }
        }
        public async Task EnsureBienniumExistsAsync(Guid id)
        {
            var biennium = await _bienniumRepository.GetByIdAsync(id);
            if (biennium == null) throw new KeyNotFoundException("Il biennio specificato non esiste.");
        }
    }
}
