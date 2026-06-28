export const environment = {
  production: true,
  // Empty = same-origin relative calls (/api/...). In production the SPA is served
  // behind the same ingress/route as the API. Override here for a split deployment.
  apiBaseUrl: '',
  // Keycloak (OIDC) — disabled by default (stretch goal). Flip enabled to true
  // (with a running Keycloak) to require login.
  auth: {
    enabled: false,
    url: 'http://localhost:8081',
    realm: 'nrs',
    clientId: 'nrs-spa',
  },
};
