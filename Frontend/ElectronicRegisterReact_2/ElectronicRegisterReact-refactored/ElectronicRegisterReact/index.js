import { registerRootComponent } from 'expo';
import App from './App';

// registerRootComponent calls AppRegistry.registerComponent('main', () => App).
// Assicura inoltre che l'ambiente sia impostato correttamente sia in Expo Go
// sia in una build nativa.
registerRootComponent(App);
