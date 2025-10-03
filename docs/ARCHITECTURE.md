# LandingPage Architecture (Azure Functions .NET 9, Isolated)

## Executive Summary
- Single Azure Function (HTTP trigger) returns a 302 redirect based on day of week.
- Tue/Thu/Sat → redirect to https://xkcd.com
- Sun/Mon/Wed/Fri → fetch random APOD from https://apod.nasa.gov/apod/archivepix.html and redirect.
- Fallback on APOD errors: https://apod.nasa.gov/apod/astropix.html
- No caching. Simpler, consistent, ~bash cost; relies on .NET 9 cold start and HTTP performance.

## Technology Stack
- Azure Functions runtime: v4 (Consumption)
- Model: .NET Isolated Worker
- Target Framework: .NET 9 (net9.0)
- Language: C# 12
- Packages:
  - Microsoft.Azure.Functions.Worker (>= 2.0.0)
  - Microsoft.Azure.Functions.Worker.Sdk (>= 2.0.0, OutputItemType=Analyzer)
  - Microsoft.Azure.Functions.Worker.Extensions.Http (>= 3.2.0)
  - HtmlAgilityPack (latest 1.11.x)
  - Optional (ASP.NET Core integration): Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore (>= 2.0.0)

## Version Matrix (Required)
- Target framework: net9.0
- Functions runtime: 4.x
- Core Tools (local): 4.x (recommend 4.0.5000+)
- App settings (Azure):
  - FUNCTIONS_WORKER_RUNTIME = dotnet-isolated
  - Linux: linuxFxVersion = DOTNET-ISOLATED|9.0
  - Windows: netFrameworkVersion = v9.0

Note: Using Worker 1.x/Worker.Sdk 1.x with net9.0 will fail with an incompatible framework error. Use 2.x for .NET 9.

## Rationale: Direct Fetch, No Cache
- Consistent latency (~200ms) for APOD days; ~60ms for xkcd days.
- Fewer moving parts vs. caching (no timers, no storage, fewer failure modes).
- Always-fresh content.

## Request Flow
1. Receive HTTP request.
2. Determine day-of-week (UTC).
3. Tue/Thu/Sat → 302 redirect to https://xkcd.com
4. Otherwise → fetch APOD archive, select random entry, 302 redirect to the entry.
5. On any failure → 302 redirect to https://apod.nasa.gov/apod/astropix.html

## Error Handling
- HTTP timeout for APOD archive fetch: 10 seconds.
- Parse errors/empty results: fallback URL.
- Log warnings for non-fatal fallbacks.

## Security / Cost Protection
- Enforce Microsoft Entra ID authentication at the Function App level (Easy Auth).
- Prevents anonymous abuse and unbounded execution.

## Intended Project Structure (reference)
LandingPage/
├─ LandingPage.csproj
├─ host.json
├─ local.settings.json            # local only (gitignored)
├─ Program.cs                     # isolated worker startup
├─ Functions/
│  └─ RedirectFunction.cs         # HTTP trigger
└─ Services/
   └─ ApodService.cs              # APOD archive scrape + selection

(Implementation files may be absent during documentation-only phases.)

## Success Criteria
- APOD redirect < 250 ms on average
- xkcd redirect < 60 ms on average
- 302 redirects across all flows; graceful fallback coverage
- Auth required at Function App level

## References
- Azure Functions .NET isolated guide (Functions 4.x + .NET 9 supported)
- HtmlAgilityPack
