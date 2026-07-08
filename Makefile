# Build & test entry points for the API2Convert .NET SDK.
#
#   make check          # restore + build + unit + security (offline; the CI guardrail)
#   make examples       # compile-check the runnable examples (guards guides vs API drift)
#   make test           # offline unit suite
#   make test-security  # independent security suite (real loopback servers)
#   make test-live      # live conformance (needs API2CONVERT_API_KEY, e.g. the behat default key)
#   make pack           # produce the NuGet package
#   make docker-check   # run `make check` inside the official .NET SDK image

SLN := Api2Convert.sln
CONFIG ?= Release

.PHONY: restore build test test-security test-live check examples pack format docker-check clean

restore:
	dotnet restore $(SLN)

build: restore
	dotnet build $(SLN) -c $(CONFIG) --no-restore

test: build
	dotnet test test/Api2Convert.Tests -c $(CONFIG) --no-build

test-security: build
	dotnet test test/Api2Convert.SecurityTests -c $(CONFIG) --no-build

# Live conformance auto-skips unless API2CONVERT_API_KEY is set. To run it against the API:
#   API2CONVERT_API_KEY=<behat default key> make test-live
# Point at another environment with API2CONVERT_BASE_URL if the key is scoped to one.
test-live: build
	dotnet test test/Api2Convert.LiveTests -c $(CONFIG) --no-build

# The guardrail: everything that must pass offline, without an API key.
check: build test test-security

# Compile-check the runnable examples against real src (via <ProjectReference>) so an API drift
# breaks the build instead of silently rotting the copy-paste guides. The examples project is
# intentionally outside Api2Convert.sln, so it is deliberately NOT part of `check`/`docker-check`.
examples:
	dotnet build examples/Examples/Examples.csproj -c $(CONFIG)

pack: build
	dotnet pack src/Api2Convert/Api2Convert.csproj -c $(CONFIG) --no-build -o artifacts

format:
	dotnet format $(SLN)

# Run the offline guardrail inside a container on a pinned .NET SDK (no local SDK needed).
docker-check:
	docker build -t api2convert-dotnet .
	docker run --rm api2convert-dotnet

clean:
	dotnet clean $(SLN) || true
	rm -rf artifacts
