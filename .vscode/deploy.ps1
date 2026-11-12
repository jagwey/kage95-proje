$ErrorActionPreference = 'Stop'

Write-Host 'Production build started...'
& npm.cmd run build
if ($LASTEXITCODE -ne 0) {
    throw 'Production build failed. Nothing was pushed.'
}

$changes = git status --short
if ($changes) {
    git add -A
    git commit -m "Deploy site" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
    if ($LASTEXITCODE -ne 0) {
        throw 'Commit failed. Nothing was pushed.'
    }
}

Write-Host 'Pushing to main...'
git push origin main
if ($LASTEXITCODE -ne 0) {
    throw 'Push failed. Check GitHub authentication and try again.'
}

Write-Host 'Deploy triggered. GitHub Actions will publish the site shortly.'
