# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 9-based Azure Function that provides day-based redirects to either xkcd.com or NASA's Astronomy Picture of the Day (APOD). The service redirects based on day of week:
- **xkcd days** (Tue, Thu, Sat): Direct redirect to https://xkcd.com
- **APOD days** (Sun, Mon, Wed, Fri): Direct fetch and redirect to a random APOD from the archive

## Architecture Decision: Direct Fetch (No Caching)

**Key Insight**: .NET 9's ~50ms cold starts make direct fetching faster and simpler than caching.

- **Performance**: ~200ms consistent response time (50ms cold start + 150ms fetch)
- **Simplicity**: Single function, no cache service, no timer functions  
- **Reliability**: Always fresh content, self-healing, fewer failure modes
- **Cost**: Essentially $0/month for personal use

## Development Commands

### Prerequisites
- .NET 9 SDK
- Azure Functions Core Tools v4
- Azure CLI

### Build and Run
```bash
# Build the project
dotnet build

# Run locally
func start

# Test the redirect endpoint locally
curl -I http://localhost:7071/api/redirect
```
