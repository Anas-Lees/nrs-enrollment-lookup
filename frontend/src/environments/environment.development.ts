export const environment = {
  production: false,
  // Empty = relative /api calls, which the dev-server proxy (proxy.conf.json)
  // forwards to the backend at http://localhost:5000.
  apiBaseUrl: '',
  // Keycloak (OIDC) — disabled by default (stretch goal).
  auth: {
    enabled: false,
    issuer: 'http://localhost:8081/realms/nrs',
    clientId: 'nrs-spa',
  },
};
