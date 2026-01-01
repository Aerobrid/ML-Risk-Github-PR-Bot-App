# Deployment Risk Platform

## Demo
<img width="1856" height="877" alt="Screenshot 2025-12-28 185801" src="https://github.com/user-attachments/assets/0fc82e14-8fb1-45bb-891e-0cba9beee8df" />
<img width="1855" height="875" alt="Screenshot 2025-12-28 204914" src="https://github.com/user-attachments/assets/44b64212-dc0b-4c69-ba29-1c9492b7c7d0" />

## Info
A lightweight platform that analyzes code changes and posts risk assessments to pull requests. It consists of three main components:

- Backend (`backend/DeploymentRisk.Api`): .NET 10 Web API that handles GitHub App authentication, webhooks, model management, and risk scoring
- Frontend (`frontend/deployment-risk-ui`): Angular UI for browsing repositories, models, and the dashboard
- ML service (`ml-service`): FastAPI Python service used to host, train, and serve ML models for risk scoring

## Goals

- Create neat personal PR assistant
- Learn about GitHub webhooks and run risk assessments on PRs
- Allow local and containerized development of the backend, frontend, and ML service through docker-compose
- Train ML model for Risk Score analysis/prediction
- Learn about Angular + ASP.NET Core in the process

## Exclaimers

- More specific readme's can be found in frontend, backend, and ml-service folders of app
- Dependency conflicts may occur for newer packages
- LLM configuration through API key has not been thoroughly tested

## Repository Layout

- `backend/DeploymentRisk.Api` — .NET 10 API project (Program.cs, Controllers, Services, Data)
- `frontend/deployment-risk-ui` — Angular front-end app
- `ml-service` — FastAPI app with training scripts and model management
- `docker-compose.yml` — compose file to run components together
- `secrets/` — local secrets directory (not checked in by default)

## Prerequisites

- .NET 10 SDK (dotnet) for backend development
- Python 3.10+ with pip for the ML service
- Node.js 18+ and npm for the frontend
- Docker & Docker Compose (optional, recommended for running full stack)
- Angular framework installed
- ngrok for local testing by tunneling your localhost -> online web

## Quickstart — Local (recommended for development)

1. Copy or create a `.env` file at the repository root. See `.env.example` for keys used by the app. At a minimum you should set up a **GitHub app** for repositories you want to give the app access to and a **github oauth app** for a secure login. More information on hooking them up is further down.

2. Populate `./secrets` in the root directory with your github app private .pem key file

3. Backend: run from its folder

```powershell
cd backend/DeploymentRisk.Api
dotnet build
dotnet run 
```

The backend uses `DotNetEnv` to load `.env` values (if present). Configuration keys follow the double-underscore mapping (for example `GitHub__PrivateKeyPath`).

4. ML service: create a virtual environment, install requirements, and run

```powershell
cd ml-service
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

4. Frontend: start the Angular app

```powershell
cd frontend/deployment-risk-ui
npm install
npm start
```

Open the frontend (usually at `http://localhost:4200`) and the backend Swagger for api endpoints at `http://localhost:5000/swagger`.

## Quickstart — Docker Compose

1. Populate `./secrets` with your `github-app.pem` and/or create a `.env` file in the repo root with the required variables.

2. Start services

```powershell
docker-compose up --build
```

3. Services will be available at the ports defined in `docker-compose.yml` (by default backend:5000, ml-service:8000, frontend:4200 when built into a container, so using ngrok here if you have no spare domain name is a good idea).

## Important Configuration (Secrets)

- `GitHub__AppId` — your GitHub App ID
- `GitHub__InstallationId` — installation id for the app
- `GitHub__PrivateKeyPath` — path to the PEM private key. When running locally, this is common: `./secrets/github-app.pem`
- `ML__ServiceUrl` — URL for ML service (default `http://localhost:8000`)

## Docker / Compose note about DB host

When you run the stack with `docker compose`, each service runs inside its own container and they communicate over an internal Docker network. That means:

- From your host machine, you may connect to the database at `localhost:1433`.
- From a container (for example the `backend` container) the database is *not* reachable at `localhost` — `localhost` inside the container refers to the container itself.

Therefore, when using `docker compose` you should point your connection string at the Compose service name `sqlserver` (the service defined in `docker-compose.yml`). Example:

Database__ConnectionString=Server=sqlserver,1433;Database=DeploymentRiskDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;

If you previously had `Server=localhost,1433` in your `.env`, replace it before starting the Compose stack. The PowerShell command below will back up your `.env` and perform the replacement:

```powershell
Copy-Item .env .env.bak
(gc .env) -replace 'Server=localhost,1433','Server=sqlserver,1433' | Set-Content .env
```

If instead you want containers to reach a database running on your host (not managed by Compose) use `host.docker.internal` on Windows:

Database__ConnectionString=Server=host.docker.internal,1433;Database=DeploymentRiskDb;User Id=sa;Password=YOUR_SA_PASSWORD;TrustServerCertificate=True;

## Untracking a tracked secrets file (if you committed secrets earlier)

```powershell
git rm --cached backend/DeploymentRisk.Api/appsettings.json
git commit -m "Remove secrets from tracked appsettings"
```

If secrets were pushed to a remote, rotate them immediately and consider a history rewrite (BFG or git-filter-repo).

## Github Oauth App Setup

1. Create a GitHub Oauth App in your organization or user account. 
2. Urls:
   - `Homepage URL`: `http://localhost:4200` or whatever port your frontend is listening to
   - `Authorization callback URL`: `http://localhost:4200/auth/callback` (default)
3. Make note of clientId and clientsecret to paste into your own `appsettings.json` and `.env`.

## GitHub App Setup

1. Create a GitHub App in your organization or user account. Set callback/webhook URLs to your backend's public address (or use `ngrok` for local testing if you cannot use localhost).
2. Urls:
   - `Homepage URL`: `http://localhost:4200`
   - `Callback URL`: Leave Empty
   - `Setup URL (optional)`: Can put to `http://localhost:4200/settings`
   - `Webhook URL`: `your-ngrok-domain/api/github/webhook` (need to use ngrok not localhost)
3. Generate and download the private key (`*.pem`) and place it in `./secrets/github-app.pem` (or point `GitHub__PrivateKeyPath` to its path).
4. Note the App ID and Installation ID and put them in your `.env` or environment.
5. Paste in the webhook secret from `appsettings.json` into github app.
6. Give the App the following permissions for full feature support (adjust minimal permissions for production):
   - Pull requests: Read & write (to post comments)
   - Contents: Read
   - Code scanning alerts: Read (if using CodeQL/code-scanning features)
7. Install the App on target repositories or the organization.

CAUTION: If you get `Octokit.ForbiddenException: Resource not accessible by integration` when fetching code-scanning alerts, it's usually a permissions issue — re-check the App permissions and reinstall the app.

## Database

The backend uses EF Core with a SQL Server provider by default. For local development you can:

- Provide `Database__ConnectionString` via `.env` to point at a SQL Server instance (Docker, Azure, or local)
- If migrations fail at startup, the app falls back to `EnsureCreated()` to create the database schema automatically for development

## Troubleshooting

- Private key not found / parse errors:
  - Ensure `GitHub__PrivateKeyPath` points to a valid PEM file and the process can read it
  - Verify the file is PEM format (starts with `-----BEGIN PRIVATE KEY-----`)
  - Can be because of no CRLF (carriage return line feeds) separating header, body, and footer

- ML service errors:
  - Start `uvicorn` without `--reload` for stable testing.
  - Check `ml-service/requirements.txt` matches installed packages.

- Code scanning 403 (CodeQL):
  - Ensure the GitHub App has Code scanning read permissions, then reinstall.

## Kubernetes Deployment

- **Overview:** : Use Kubernetes for production deployments with Docker Compose. This project includes a sample manifest for a cluster under `linode/full-deploy.yaml` which is intentionally sanitized of secrets in the repo and built for linode platform. Put production secrets into Kubernetes Secrets or a secret manager and never commit them.

- **Secrets (recommended):** : create secrets with `kubectl` or your cloud provider's secret store. Example (kubectl, creates `risk-secrets` in namespace `risk`):

```powershell
# create namespace
kubectl create namespace risk

# create secret from literals (recommended for CI):
kubectl create secret generic risk-secrets \
   --from-literal=SQL_SA_PASSWORD='YourStrong!Passw0rd' \
   --from-literal=GITHUB_APP_ID='2540120' \
   --from-literal=GITHUB_WEBHOOK_SECRET='your-webhook-secret-here' \
   --from-literal=GITHUB_TOKEN='ghp_your_token_here' \
   --namespace risk
```

- **Apply sanitized manifests:** : after creating secrets and confirming your `kubeconfig` points at the target cluster, apply the manifest:

```powershell
# from repo root
kubectl apply -f linode/full-deploy.yaml -n risk
```

- **Image pull secrets for private registries:** : if images are stored in GHCR/AWS ECR/ACR, create an image pull secret and reference it in the Deployment `imagePullSecrets` (the sample already references `ghcr-secret`). Example for GHCR with a personal access token:

```powershell
# create docker-registry secret for GHCR
kubectl create secret docker-registry ghcr-secret \
   --docker-server=ghcr.io \
   --docker-username=YOUR_GH_USERNAME \
   --docker-password='YOUR_GH_PERSONAL_ACCESS_TOKEN' \
   --namespace risk
```

- **Linode (LKE) quick steps:** :
   - Create a Linode Kubernetes Engine (LKE) cluster from the Linode console.
   - Download the kubeconfig from the Linode UI and merge it into `~/.kube/config` or setup `KUBECONFIG` accordingly.
   - Create `risk` namespace and secrets as shown above, then apply `linode/full-deploy.yaml`.
   - Use `kubectl get svc -n risk` to find public LoadBalancer IPs (frontend service in the sample is `LoadBalancer`).


## Linode post-deploy notes

If you've applied `linode/full-deploy.yaml` to an LKE cluster, use these Linode-specific steps to finish setup and expose services securely.

- Get the frontend LoadBalancer IP (may take a few minutes to provision or may come already setup on the `NodeBalancer` tab):

```powershell
kubectl get svc frontend -n risk
# look for EXTERNAL-IP column
```

- Create a DNS A record in Linode DNS pointing your domain (e.g. `risk.example.com`) to the external IP. You can do this in Linode Cloud Manager → Networking → Domains or via `linode-cli`.

- Configure HTTPS with cert-manager (recommended). Example install and ClusterIssuer (LetsEncrypt staging for testing):

```bash
# install cert-manager
kubectl apply --validate=false -f https://github.com/cert-manager/cert-manager/releases/download/v1.12.0/cert-manager.yaml

# create a ClusterIssuer for Let's Encrypt staging (replace email)
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
   name: letsencrypt-staging
spec:
   acme:
      server: https://acme-staging-v02.api.letsencrypt.org/directory
      email: you@example.com
      privateKeySecretRef:
         name: letsencrypt-staging
      solvers:
      - http01:
            ingress:
               class: nginx
EOF
```

- Example Ingress (using nginx ingress controller) to front the frontend and backend and request TLS certificate (replace host and issuer):

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
   name: risk-ingress
   namespace: risk
   annotations:
      cert-manager.io/cluster-issuer: letsencrypt-staging
spec:
   rules:
   - host: risk.example.com
      http:
         paths:
         - path: /
            pathType: Prefix
            backend:
               service:
                  name: frontend
                  port:
                     number: 80
   tls:
   - hosts:
      - risk.example.com
      secretName: risk-tls
```

- GitHub webhook: set the webhook `Payload URL` to `https://risk.example.com/api/github/webhook` and ensure `GitHub__WebhookSecret` in your `risk-secrets` matches the webhook secret you configure in GitHub. Errors logging in with github oauth are mainly because of this.

- Useful commands:

```powershell
# view logs
kubectl logs -f deploy/backend -n risk

# check deployments
kubectl get deploy -n risk

# rollout a new image
kubectl set image deployment/backend backend=ghcr.io/your/repo-backend:tag -n risk
```

- Notes:
   - Setting this app up on Linode is much, much easier than setting it up on AWS EKS.
   - LKE LoadBalancers are billed and IP allocation can take a minute. I used **NodeBalancer** on linode console instead.
   - If you prefer a stable hostname without managing certs yourself, use a cloud Load Balancer + DNS + managed certs or a platform ingress add-on that provisions TLS automatically.
