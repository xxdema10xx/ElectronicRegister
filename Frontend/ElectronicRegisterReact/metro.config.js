// metro.config.js
// Proxy di sviluppo per la versione web: il browser chiama /api sulla stessa origine del dev server
// (http://localhost:8081) e Metro inoltra al backend indicato in EXPO_PUBLIC_DEV_PROXY. Serve perché il
// backend di staging accetta solo origini https (CORS), quindi da http://localhost il browser bloccherebbe
// le chiamate. Le app native non hanno CORS e continuano a usare EXPO_PUBLIC_API_BASE.
const { getDefaultConfig } = require("expo/metro-config");
const http = require("http");
const https = require("https");

const config = getDefaultConfig(__dirname);
const target = process.env.EXPO_PUBLIC_DEV_PROXY;

if (target) {
  const upstream = new URL(target);
  const client = upstream.protocol === "https:" ? https : http;

  config.server = config.server || {};
  const previous = config.server.enhanceMiddleware;
  config.server.enhanceMiddleware = (middleware, server) => {
    const base = previous ? previous(middleware, server) : middleware;
    return (req, res, next) => {
      if (!req.url.startsWith("/api/")) return base(req, res, next);
      // Origin e Referer vanno tolti: il backend li tratterebbe come richiesta cross-origin non ammessa
      const { host, origin, referer, ...headers } = req.headers;
      const proxied = client.request({
        protocol: upstream.protocol,
        hostname: upstream.hostname,
        port: upstream.port || undefined,
        method: req.method,
        path: req.url,
        headers: { ...headers, host: upstream.host },
      }, (upRes) => {
        res.writeHead(upRes.statusCode || 502, upRes.headers);
        upRes.pipe(res);
      });
      proxied.on("error", (err) => {
        res.writeHead(502, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ title: `Backend non raggiungibile (${err.code || err.message})`, status: 502 }));
      });
      req.pipe(proxied);
    };
  };
}

module.exports = config;
