- togliere la logica dai controler, nei controlle civa una riga di codice il manager che chiama tutto i flusso
- nei controller vengono inietati i manager e basta
- dalle confogurazioni non deve uscire il IConfiguration 
- se le confiogurazioni escono lo fanno solo tramite IOption<> Patern
- tutto quelo che è specifico e strettamente legato all'impelenmtazione dell'infrastruttura va messo dentro il livello infrastrutturale
- Vanno creati almeno 4 livelli, uno di dominio con modelli DTO e interfacce dei servizi; un dominio di infrastruttura con modelli legati all'infrastrutura (es modelli di entità del DB)
- La dependency injection deve avvenire sempre tramite interfacce e poi conccrete (es addscope<IService, ConcreteService>)
- la dependency ingection deve avvenire all'interno delle librerie specifiche utilizando il ServiceCollectionProvider:

using microsoft.dependencinjection

public static ServiceCollection AddspecificService(this IServiceCollection service)
{
 service.addscope<IService, ConcreteService>()
}

- le classi devono essere tutte internal
- i modelli devono avere ogniuno la sua classe, lo stesso vale per gli enum
- usa sempre in DI il lifecycle più breve possibile, prediligendo lo scope al transient


indentatura delle cartelle: 
root
    solution
    src
        Application
            librerie di application
        Business
            librerie di business
        Infrastructure
            librerie specifiche per la gestione dell'infrastruttura
        Domain
            libbrerie che contiene modelli interfacce ed astrazioni del dto e dei servizi
    test
        librerie di test