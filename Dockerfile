# syntax=docker/dockerfile:1
#
# Build and test the SDK on a pinned, supported .NET SDK — the local box may not have one.
# This image runs the guardrail (build + offline unit suite + independent security suite) on
# the version the package targets.
#
#   docker build -t api2convert-dotnet .
#   docker run --rm api2convert-dotnet                                   # `make check` on .NET 8
#   docker run --rm -e API2CONVERT_API_KEY=<key> api2convert-dotnet \
#       dotnet test test/Api2Convert.LiveTests -c Release                # opt-in live conformance
#
ARG DOTNET_VERSION=8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}

WORKDIR /sdk

# Restore first for layer caching: copy the solution + every project file, then restore.
COPY Api2Convert.sln Directory.Build.props ./
COPY src/Api2Convert/Api2Convert.csproj src/Api2Convert/
COPY test/Api2Convert.Tests/Api2Convert.Tests.csproj test/Api2Convert.Tests/
COPY test/Api2Convert.SecurityTests/Api2Convert.SecurityTests.csproj test/Api2Convert.SecurityTests/
COPY test/Api2Convert.LiveTests/Api2Convert.LiveTests.csproj test/Api2Convert.LiveTests/
RUN dotnet restore Api2Convert.sln

# Then the rest of the source.
COPY . .

# Default: build + offline unit + independent security suite — all must pass. Live conformance is
# opt-in (needs API2CONVERT_API_KEY): override the command with `dotnet test test/Api2Convert.LiveTests`.
# (Invoked directly, not via `make`, since the SDK image ships no `make`.)
CMD ["sh", "-c", "dotnet build Api2Convert.sln -c Release --no-restore && dotnet test test/Api2Convert.Tests -c Release --no-build && dotnet test test/Api2Convert.SecurityTests -c Release --no-build"]
