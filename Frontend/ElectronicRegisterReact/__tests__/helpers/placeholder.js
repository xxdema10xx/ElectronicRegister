// Schermata segnaposto per i test di navigazione: mostra solo "SCREEN <nome>"
const { Text } = require("react-native");

module.exports = (name) => function Placeholder() { return <Text>{`SCREEN ${name}`}</Text>; };
