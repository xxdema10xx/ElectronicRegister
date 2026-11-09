import { registerRootComponent } from 'expo';
import { Text, View } from 'react-native';
import App from './App';
import { CONFIG_ERROR } from './src/appConfig';
import { DialogHost } from './src/components/AppDialog';

// Radice: l'app più il contenitore dei dialog (avvisi e conferme), che deve stare sopra tutto.
function Root() {
  if (CONFIG_ERROR) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 }}>
        <Text style={{ fontSize: 16, textAlign: 'center' }}>{CONFIG_ERROR}</Text>
      </View>
    );
  }
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
