import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// startup file for rendering app component in index.html
bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
