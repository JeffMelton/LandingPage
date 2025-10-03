# LandingPage

A personal browser homepage that redirects to either xkcd or a random NASA APOD archive entry based on the day of the week. Built with Azure Functions and .NET 9.

## Why This Exists

In early 2025, federal funding cuts ended updates to NASA's Astronomy Picture of the Day (APOD), which had been my homepage for years. Around the same time, the xkcd iOS app was removed from the App Store, making it harder to follow new comics. As someone who's autistic, these disruptions to daily routines hit harder than they might for others—maintaining regular sources of awe and humor is genuinely stabilizing.

Rather than manually updating bookmarks or hunting for alternatives, I built this automated landing page. It serves a practical purpose while also giving me hands-on experience with the Azure product suite, something I'd been wanting to learn more deeply.

## What It Does

The function runs a simple day-of-week router:

- **Tuesday, Thursday, Saturday**: Redirect to [xkcd.com](https://xkcd.com) (new comic days)
- **Sunday, Monday, Wednesday, Friday**: Redirect to a pseudo-random entry from the [APOD archive](https://apod.nasa.gov/apod/archivepix.html)

Since the NASA API lost funding too, the APOD selection works by scraping the archive page, extracting all available entries, and selecting one pseudo-randomly. The first request does the scraping work and caches the archive list in Azure Blob Storage; subsequent requests just pull from that cache.

## Architecture Highlights

### Direct Fetch Over Caching

The initial plan included elaborate caching strategies, but .NET 9's ~50ms cold starts changed the calculus entirely. Direct fetching is:

- **Faster**: ~200ms total response time (50ms cold start + 150ms fetch)
- **Simpler**: Single function, no timer functions, no cache invalidation logic
- **More reliable**: Always fresh content, self-healing, fewer failure modes
- **Cost-effective**: Essentially free on Azure Consumption plan

### Tech Stack

- **.NET 9**: Chosen for performance and tight Azure integration
- **Azure Functions**: HTTP-triggered, isolated worker process, Functions v4 runtime
- **Azure Blob Storage**: Caches scraped APOD archive to avoid repeated scraping
- **Entra ID Authentication**: Restricts access to my tenant only (cost control + privacy)
- **HtmlAgilityPack**: For parsing the APOD archive HTML

### Security & Cost Protection

The function requires Microsoft Entra ID authentication at the platform level ("Easy Auth"). This prevents both abuse and surprise Azure bills—only authenticated users from my tenant can trigger executions.

## Project Structure

```
LandingPage/
├── redirect.cs                   # Main HTTP trigger function
├── Program.cs                    # Isolated worker startup & DI config
├── Models/
│   ├── ApodEntry.cs             # APOD archive entry model
│   └── RedirectTarget.cs        # Result wrapper for redirect decisions
├── Services/
│   ├── DayOfWeekRouter.cs       # Day-based routing logic
│   ├── ApodArchiveScraper.cs    # Scrapes & caches APOD archive
│   └── ApodSelector.cs          # Selects random APOD from archive
└── docs/
    ├── azure-provisioning.md    # Azure resource setup guide
    └── github-actions-setup.md  # CI/CD setup with federated credentials
```

## Running Locally

You'll need:
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (optional, for deployment)

```bash
# Build
dotnet build

# Run locally
func start

# Test
curl -I http://localhost:7071/api/redirect
```

The local version uses `local.settings.json` (gitignored) for development storage emulation.

## Deployment

This project includes comprehensive documentation for:

- **Azure resource provisioning** via `az` CLI (see `docs/azure-provisioning.md`)
- **GitHub Actions CI/CD** with OpenID Connect federated credentials (see `docs/github-actions-setup.md`)

The deployment uses a user-assigned managed identity instead of publish profiles or service principal secrets, following current Azure security best practices.

## Can I Use This?

Not as-is—it's tailored to my specific Entra ID tenant and Azure subscription. However, you're welcome to fork it and adapt it:

1. Update the Entra ID configuration to your tenant
2. Provision your own Azure resources (see `docs/azure-provisioning.md`)
3. Modify the routing logic to fit your needs (maybe you prefer different days for different sites?)
4. Deploy using the GitHub Actions workflow or manually with `func azure functionapp publish`

The documentation is intentionally detailed to serve as a learning resource for anyone exploring Azure Functions, automated deployment, or similar personal automation projects.

## Performance Characteristics

- **xkcd redirect**: ~60ms average
- **APOD redirect**: ~200ms average (first request may take longer while scraping)
- **Cold start**: ~50ms (.NET 9 isolated worker)
- **Cost**: ~$0/month on Azure Consumption plan with personal usage patterns

## License

MIT License. See [LICENSE](LICENSE) for details.

## Acknowledgments

- NASA for decades of inspiring daily images, even if the funding ran out
- Randall Munroe for xkcd, a consistent source of clever humor
- Microsoft for the Azure free tier that makes personal projects like this feasible
