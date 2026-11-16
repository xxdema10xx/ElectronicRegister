// Fuso orario fisso per i test: le date degli esami passano da UTC (server) all'ora locale (interfaccia)
module.exports = () => { process.env.TZ = "Europe/Rome"; };
