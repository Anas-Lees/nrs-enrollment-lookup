import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { loadAppConfig } from './app/core/config/app-config';

// Load runtime config (config.json) before bootstrapping so services/auth see it.
loadAppConfig().then(() => bootstrapApplication(App, appConfig).catch((err) => console.error(err)));
