# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 9-based Azure Function that provides a personal landing page with day-based redirects to either xkcd.com or NASA's Astronomy Picture of the Day (APOD) archive. This project serves as both a practical solution to maintain daily routines and a learning opportunity for Azure product suite development.

### Purpose & Motivation
- **Personal Need**: Replaces NASA APOD as daily homepage after federal funding loss ended updates
- **Secondary Goal**: Provides alternative access to xkcd content after iOS app removal from App Store
- **Learning Objective**: Develop familiarity with Azure product suite through practical implementation
- **Accessibility**: For an autistic user, maintaining daily routines and regular sources of awe/wonder is stabilizing

### Redirect Logic
- **xkcd days** (Tue, Thu, Sat): Direct redirect to https://xkcd.com (new comic publication days)
- **APOD days** (Sun, Mon, Wed, Fri): Redirect to pseudo-randomly selected entry from APOD archive

### APOD Archive Access
Since NASA public APIs lost funding, the implementation will:
1. Scrape the APOD archive page (https://apod.nasa.gov/apod/archivepix.html)
2. Extract hyperlinked entries from the archive
3. Select pseudo-randomly from available entries
4. Consider storing scraped archive in Azure Blob Storage to avoid repeated scraping (TBD)

## Architecture Decisions

### Direct Fetch (No Caching Required)
**Key Insight**: .NET 9's ~50ms cold starts make direct fetching faster and simpler than caching for real-time requests.

- **Performance**: ~200ms consistent response time (50ms cold start + 150ms fetch)
- **Simplicity**: Single function, no cache service, no timer functions
- **Reliability**: Always fresh content, self-healing, fewer failure modes
- **Cost**: Essentially $0/month for personal use with Entra ID authentication

### Technology Choices
- **.NET 9**: Chosen for speed and tight Azure integration
- **Azure Functions**: Isolated worker process, Core Services > 4.x
- **Entra ID Authentication**: Required for access control and cost management (leveraging Entra ID P2 tenant)

## Development Environment

### Host System
- Manjaro Linux
- az CLI (via podman container, mounting ~/.azure and $PWD)
- dotnet CLI (installed locally)
- func CLI (Azure Functions Core Tools, installed locally)

### Azure Resource Provisioning
**Not yet provisioned.** When needed, user prefers step-by-step instructions for manual provisioning via `az` or `azd` CLI to improve Azure workflow familiarity.

## Development Commands

### Prerequisites
- .NET 9 SDK
- Azure Functions Core Tools v4
- Azure CLI (via podman container)

### Build and Run
```bash
# Build the project
dotnet build

# Run locally
func start

# Test the redirect endpoint locally
curl -I http://localhost:7071/api/redirect
```

### Azure CLI (via podman)
```bash
# Example pattern for running az commands
podman run --rm -v ~/.azure:/root/.azure:Z -v $PWD:/workspace:Z <image> az <command>
```
