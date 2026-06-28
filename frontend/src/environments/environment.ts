export const environment = {
  production: true,
  // Empty = same-origin relative calls (/api/...). In production the SPA is served
  // behind the same ingress/route as the API. Override here for a split deployment.
  apiBaseUrl: '',
  // Keycloak (OIDC) — disabled by default (stretch goal). Flip enabled to true and
  // wire a real OIDC adapter to require login.
  auth: {
    enabled: false,
    issuer: 'http://localhost:8081/realms/nrs',
    clientId: 'nrs-spa',
  },
};
