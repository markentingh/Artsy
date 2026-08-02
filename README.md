# Artsy Platform

Artsy is an AI-powered print-on-demand platform that automates the entire workflow from trend research and artwork generation through product creation, publishing, and social media marketing. It integrates with OpenAI for image generation, Printify for print-on-demand fulfillment, Instagram for social media posting, Telegram for notifications, and SerpApi for trend research.

---

## Prerequisites

Before setting up Artsy, ensure you have the following:

- **.NET 9 SDK** or later — [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- **PostgreSQL** — a running server with a login that can create databases
- **Node.js LTS** — required for the web client build tooling and setup scripts
- **npm** — installed with Node.js
- **Git** — for cloning the repository
- **Visual Studio 2022** or `dotnet` CLI — for building and running the .NET projects

### External Service Accounts

You will need accounts and credentials for the following services:

- **OpenAI** — API key for image generation (DALL-E) and AI-powered description generation
- **Printify** — API token and OAuth client credentials for print-on-demand product creation and publishing
- **Meta/Facebook for Developers** — Facebook Login app and Instagram Graph API app for social media posting
- **Telegram** — Bot token from BotFather for notifications
- **SerpApi** — API key for trend research
- **SendGrid** (optional) — API key for transactional emails
- **Azure Blob Storage** (optional) — connection string if using cloud storage instead of local filesystem

---

## Quick Start

```bash
git clone https://github.com/Artsy/Framework
cd Framework
setup.bat
```

`setup.bat` prompts for a project prefix, PostgreSQL credentials, ports, and SendGrid defaults, then handles renaming, dependency installs, database creation, and schema deployment automatically. After it finishes, run the API server and Vite dev proxy, create the first admin account, and log into the dashboard.

---

## Configuration

Copy `appsettings.template.json` to `appsettings.json` in the `Artsy.Web.Server` project and fill in your credentials. The template mirrors the full structure of `appsettings.json` with placeholder values.

### Connection Strings

| Key | Description |
|-----|-------------|
| `ConnectionStrings:Database` | PostgreSQL connection string (host, database, username, password) |

### Auth / JWT

| Key | Description |
|-----|-------------|
| `Auth:Domain` | The domain used for authentication callbacks |
| `Auth:JWT:Secret` | A strong secret key of at least 32 characters |
| `Auth:JWT:ExpiryMins` | JWT token expiry in minutes (default: 1440 = 24 hours) |
| `Auth:JWT:RefreshExpiryMins` | Refresh token expiry in minutes (default: 10080 = 7 days) |

### Printify

| Key | Description |
|-----|-------------|
| `Printify:ClientId` | OAuth client ID from Printify |
| `Printify:SecretKey` | OAuth client secret from Printify |
| `Printify:ApiToken` | Printify API token for direct API access |
| `Printify:Images:Domain` | Public-facing domain for serving product images (used in Printify mockups and product images) |

### Image Generation (OpenAI)

| Key | Description |
|-----|-------------|
| `ImageGeneration:TimeoutSeconds` | Timeout for image generation requests (default: 300) |
| `ImageGeneration:Models:OpenAI:ApiKey` | OpenAI API key |
| `ImageGeneration:Models:OpenAI:Endpoint` | OpenAI responses API endpoint |
| `ImageGeneration:Models:OpenAI:ImageEndpoint` | OpenAI image generation endpoint |
| `ImageGeneration:Models:OpenAI:ImageEditEndpoint` | OpenAI image edit endpoint |

### Upscaler

| Key | Description |
|-----|-------------|
| `Upscaler:Endpoint` | URL of the Artsy.Upscaler service (default: `http://localhost:7725`) |

### Meta (Facebook / Instagram)

| Key | Description |
|-----|-------------|
| `Meta:FacebookLogin:AppId` | Facebook Login app ID |
| `Meta:FacebookLogin:AppSecret` | Facebook Login app secret |
| `Meta:FacebookLogin:RedirectUri` | OAuth callback URL for Facebook Login |
| `Meta:Instagram:AppId` | Instagram Graph API app ID |
| `Meta:Instagram:AppSecret` | Instagram Graph API app secret |
| `Meta:Instagram:RedirectUri` | OAuth callback URL for Instagram |
| `Meta:Images:Domain` | Public-facing domain for serving images to Instagram (must be publicly accessible) |
| `Meta:Threads:AppId` | Threads API app ID (optional) |
| `Meta:Threads:AppSecret` | Threads API app secret (optional) |
| `Meta:WhatsApp:AppId` | WhatsApp Business API app ID (optional) |
| `Meta:WhatsApp:AppSecret` | WhatsApp Business API app secret (optional) |
| `Meta:Messenger:AppId` | Messenger API app ID (optional) |
| `Meta:Messenger:AppSecret` | Messenger API app secret (optional) |
| `Meta:Pages:AppId` | Facebook Pages API app ID (optional) |
| `Meta:Pages:AppSecret` | Facebook Pages API app secret (optional) |

### Telegram

| Key | Description |
|-----|-------------|
| `Telegram:BotToken` | Bot token from Telegram BotFather |
| `Telegram:BotUsername` | Bot username (without @) |

### Etsy

| Key | Description |
|-----|-------------|
| `Etsy:Keystring` | Etsy API keystring |
| `Etsy:SharedSecret` | Etsy shared secret |

### SerpApi

| Key | Description |
|-----|-------------|
| `SerpApi:ApiKey` | SerpApi key for Google Trends research |

### SendGrid

| Key | Description |
|-----|-------------|
| `SendGrid:UseSendGrid` | Enable SendGrid for transactional emails (true/false) |
| `SendGrid:SendGridApiKey` | SendGrid API key |
| `SendGrid:DefaultFromEmail` | Default sender email address |
| `SendGrid:DefaultFromName` | Default sender display name |
| `SendGrid:TrackingEmail` | Optional tracking email for bounces/complaints |

### Storage

| Key | Description |
|-----|-------------|
| `Storage:Active` | Storage backend: `filesystem` or `azureblob` |
| `Storage:AzureBlob:ConnectionString` | Azure Blob connection string (if using Azure) |
| `Storage:AzureBlob:ContainerName` | Azure Blob container name (if using Azure) |

---

## Reverse Proxy Requirement for Local Testing

Both `Printify:Images:Domain` and `Meta:Images:Domain` must point to publicly accessible URLs because Printify and Instagram fetch images from external URLs. Additionally, the Instagram OAuth redirect URI (`Meta:Instagram:RedirectUri`) must use HTTPS with a valid SSL certificate, which means it also requires a reverse proxy when testing locally. You will need a reverse proxy (such as ngrok, Cloudflare Tunnel, or a custom domain with nginx/Caddy) to expose your local server to the internet with SSL. The exact redirect URI must also be registered in your Meta App Dashboard under Instagram Graph API settings.

---

## Platform Features

### Project Management

- Create and manage projects with custom titles and keys
- Each project contains a collection of items (products/artworks)
- Project-level questionnaires for AI-driven artwork generation
- Project checklist to track overall progress
- Archive and unarchive projects

### Collection Wizard

The collection wizard is a multi-step workflow that guides users through the entire process:

1. **Project Questions** — Answer project-level questions that inform AI artwork generation
2. **Artwork Questions** — Answer item-specific questions for each product
3. **Ready to Upscale** — Generate AI artworks using OpenAI and upscale them to 4K
4. **Create Products** — Upload artworks to Printify and create products with mockups
5. **Product Images** — Generate AI-powered product images with customizable prompts
6. **Publish Products** — Publish products to Printify and manage their status
7. **Social Media** — Select images, write descriptions, and post carousels to Instagram
8. **Summary** — Review all published products, social media posts, and images in one place

### AI Image Generation

- OpenAI-powered artwork generation from text prompts
- AI-generated product images with customizable prompts
- Image upscaling to 4K resolution via the Artsy.Upscaler service
- Image resizing and cropping for Instagram (1080x1350)
- Multiple AI model support with local and cloud options

### Printify Integration

- Browse and search the Printify product catalog
- Configure blueprints with specific variants and placements
- Automatically create products from generated artworks
- Download and display Printify mockups
- Publish and unpublish products to Printify
- View products on Printify directly from the platform

### Instagram Integration

- Connect Instagram Business accounts via OAuth
- Create multi-image carousel posts with up to 10 images
- AI-generated post descriptions
- Automatic image resizing and cropping for Instagram requirements
- Media readiness polling before publishing
- Store and display Instagram post permalinks
- View posts on Instagram directly from the platform

### Telegram Integration

- Connect a Telegram bot for notifications
- Send messages and alerts to connected users
- Webhook configuration for receiving messages

### Trend Research

- Google Trends research powered by SerpApi
- Real-time trend analysis with sparkline visualizations
- SignalR hub for live research progress updates
- Save and manage trend research results

### User Management

- Admin dashboard for managing users
- Role-based access control (admin, user)
- Password reset via SendGrid email
- User filtering, sorting, and pagination

### Connections & Services

- Centralized connections page for managing Printify, Telegram, and Instagram integrations
- Admin services page for managing Printify catalog, webhooks, and Telegram bot settings
- OpenAI model management (add, enable, set preferred models)

### Additional Features

- Dark mode support throughout the UI
- Responsive design with TailwindCSS
- Real-time updates via SignalR (trend research, collection wizard)
- Image carousel and preview modals
- Configurable storage backend (filesystem or Azure Blob)
- JWT authentication with rolling refresh tokens
- First registered account automatically becomes admin
