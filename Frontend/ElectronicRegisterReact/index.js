// PUNTO DI PARTENZA DELL'APP: è il primo file che viene eseguito quando si avvia il frontend.

// Prende da "expo" lo strumento che dice al sistema "questo è il componente principale dell'app".
import { registerRootComponent } from 'expo';
// Prende due "tessere" grafiche: Text (testo) e View (scatola/contenitore). (vedi GUIDA §4.4)
import { Text, View } from 'react-native';
// Importa App, la schermata-cervello definita nel file App.js.
import App from './App';
// Importa CONFIG_ERROR: un messaggio di errore che esiste solo se la configurazione dell'app è sbagliata.
import { CONFIG_ERROR } from './src/appConfig';
// Importa DialogHost: il "contenitore" che mostra le finestrelle di avviso e di conferma.
import { DialogHost } from './src/components/AppDialog';

// Radice: l'app più il contenitore dei dialog (avvisi e conferme), che deve stare sopra tutto.
// "Root" è un componente (vedi GUIDA §4.3): una funzione che restituisce un pezzo di schermata.
function Root() {
  // Se la configurazione è sbagliata, inutile mostrare l'app: si mostra solo il messaggio d'errore.
  if (CONFIG_ERROR) {
    return (
      // Scatola che occupa tutto lo schermo (flex: 1) e centra il contenuto in orizzontale e verticale.
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 }}>
        {/* Il testo dell'errore, centrato; le graffe { } inseriscono il valore della variabile. */}
        <Text style={{ fontSize: 16, textAlign: 'center' }}>{CONFIG_ERROR}</Text>
      </View>
    );
  }
  // Se tutto è a posto: una scatola a schermo intero con dentro l'app vera e il contenitore dei dialog.
  return (
    <View style={{ flex: 1 }}>
      {/* L'app vera (login, menu, schermate…). */}
      <App />
      {/* Messo DOPO App, così le finestrelle compaiono sopra a tutto il resto. */}
      <DialogHost />
    </View>
  );
}

// registerRootComponent calls AppRegistry.registerComponent('main', () => Root).
// Assicura inoltre che l'ambiente sia impostato correttamente sia in Expo Go
// sia in una build nativa.
// In parole semplici: "avvia l'app partendo dal componente Root".
registerRootComponent(Root);
