# Deployment Risk UI

This is the Angular frontend for the Deployment Risk Platform. It provides a web UI to browse repositories, view risk assessments, and manage ML models.

Prerequisites

- Node.js 18+ and npm
- Angular CLI (optional globally) `npm i -g @angular/cli`

Quick start — local development

1. Install dependencies

```bash
cd frontend/deployment-risk-ui
npm install
```

2. Configure backend URL

The frontend reads its API base URL from the environments. Edit `src/environments/environment.development.ts` or `src/environments/environment.ts` to point to your backend API (default `http://localhost:5000`). Example:

```ts
export const environment = {
	production: false,
	apiBaseUrl: 'http://localhost:5000/api'
};
```

There is also an `app.config.ts` in the app source that can be used for runtime config.

3. Start the dev server

```bash
npm start
# or
ng serve
```

Open `http://localhost:4200` to view the app. The dev server will hot-reload on file changes.

Building for production

```bash
npm run build
# output in dist/ directory
```

Running with a proxy (optional)

If you want the dev server to proxy API calls to the backend without CORS changes, create `proxy.conf.json` and run `ng serve --proxy-config proxy.conf.json`. Example proxy:

```json
{
	"/api": {
		"target": "http://localhost:5000",
		"secure": false,
		"changeOrigin": true
	}
}
```

Docker / Docker Compose

If you prefer containers, the repository has a `docker-compose.yml` that can run the full stack. Ensure the backend and ML service are reachable by the frontend container (adjust URLs or environment variables as needed).

Common tasks

- Run unit tests: `npm test` or `ng test`
- Generate component: `ng generate component <name>`

Troubleshooting

- API 401/403: ensure the backend is running and the frontend `apiBaseUrl` points to the correct backend address.
- CORS errors: either enable CORS on the backend or use a local proxy (`proxy.conf.json`) during development.
- Not seeing model/PR data: confirm the backend has a working GitHub App configuration and the ML service is running at the configured URL.