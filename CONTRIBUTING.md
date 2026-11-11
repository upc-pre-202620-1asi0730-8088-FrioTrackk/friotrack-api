# Contribution workflow

Create a `feature/<purpose>` branch from `develop`. Implement a coherent change and keep the API contract, tests and documentation aligned. Open a pull request into `develop` with the problem, resulting behavior and validation results.

Use Conventional Commits such as `feat(shipments): validate resource availability`, `fix(auth): reject expired sessions` or `refactor(api): isolate file persistence`. Each commit should describe a complete change that can be reviewed independently. Use your own Git identity; keep contributors' actual work attributable in the history.

Before requesting review, run:

```powershell
dotnet publish FrioTrack.Api.csproj -c Release -o output/api-publish --no-self-contained
node scripts/test-api.mjs
git diff --check
```

Do not commit private stores, tokens, generated account files, deployment credentials, build outputs or local verification receipts. Synthetic fixtures and disposable test credentials belong to the local suite.

Cut `release/<version>` from `develop`, prepare accurate release notes and integrate the reviewed release into `main`. Tag the exact reviewed commit and publish its release with the verified scope and known limitations. Return the integration to `develop`. Emergency `hotfix/<purpose>` branches start from `main` and are merged into both `main` and `develop`.

During the transition from `friotrack-frontend/api`, keep the C# files and demonstration seed consistent in both locations. The independent API repository is the canonical source. Source publication and service deployment are separate actions; release notes should state which revision was actually deployed.
