export const environment = {
  production: true,
  // Empty = same-origin relative calls (/api/...). In production the SPA is served
  // behind the same ingress/route as the API. Override here for a split deployment.
  apiBaseUrl: '',
};
