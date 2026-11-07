import { registerRootComponent } from 'expo';
import { View } from 'react-native';
import App from './App';
import { DialogHost } from './src/components/AppDialog';

// Radice: l'app più il contenitore dei dialog (avvisi e conferme), che deve stare sopra tutto.
function Root() {
  return (
    <View style={{ flex: 1 }}>
      <App />
      <DialogHost />
    </View>
  );
}

// registerRootComponent calls AppRegistry.registerComponent('main', () => Root).
// Assicura inoltre che l'ambiente sia impostato correttamente sia in Expo Go
// sia in una build nativa.
registerRootComponent(Root);
