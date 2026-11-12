param(
    # The loader folder made by `python3 installer\loader_inputs.py fetch --out DIR` (stage.py leaves one in <Staged>\loader).
    # It must hold exactly the files in installer\loader-inputs.json with the listed hashes; nothing from a lab machine is trusted.
    [string]$LoaderRoot,
    # A Hearthstone client folder. Every payload file is compared with it by hash, including BepInEx\core. Required: without it nothing is compared.
    [Parameter(Mandatory=$true)][string]$ClientReference,
    # A live-test build. It must carry the hooks; a release must have none, and that is checked on every path, -Staged included.
    [switch]$HookBuild,
    [string]$Dotnet='dotnet',
    [string]$Version='0.1.0-prototype',
    # A folder made by installer\stage.py: launcher\, plugin\, release.json, NOTICE.txt and legal\ (licence texts, SOURCE.txt). The dotnet builds are skipped.
    [string]$Staged,
    # An existing ISCC.exe. Without it the pinned Tools.InnoSetup package is restored with dotnet.
    [string]$Iscc,
    [string]$OutDir,
    # Stop after the update zip (a release that only the update path needs).
    [switch]$NoSetup,
    # Build inputs for a non-staged build. Public keys only. Empty means updates are not configured.
    [string]$RootKey='', [string]$SigningKey='', [string]$Repo='', [string]$SeqFloor='0',
    # A folder with game\ and bepinex\ (your client's Managed files, BepInEx.dll, 0Harmony.dll) for the plugin build of a non-staged run.
    # Default: references\ next to plugin\.
    [string]$References
)
$ErrorActionPreference='Stop'
$product=Split-Path $PSScriptRoot -Parent
$out=if ($OutDir) { [IO.Path]::GetFullPath($OutDir).TrimEnd('\') } else { Join-Path $PSScriptRoot 'out' }
$app=Join-Path $out 'app'
function Run-Dotnet([string[]]$Arguments) {
    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE)" }
}
function Pin([string]$Path,[string]$Expected) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ine $Expected) { throw "Hash mismatch: $(Split-Path $Path -Leaf)" }
}
function Assert-Regular([string]$Path) {
    $item=Get-Item -LiteralPath $Path -Force
    while($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked build inputs/outputs are refused' }
        $item=if ($item -is [IO.FileInfo]) { $item.Directory } else { $item.Parent }
    }
}
if (-not $LoaderRoot) {
    if (-not $Staged) { throw 'Pass -LoaderRoot (a folder from loader_inputs.py fetch) or a -Staged folder that has loader\' }
    $LoaderRoot=Join-Path $Staged 'loader'
}
$LoaderRoot=[IO.Path]::GetFullPath($LoaderRoot).TrimEnd('\')
foreach($path in @($LoaderRoot,$out)) {
    # The lab's real install and accepted account, written in pieces so this file does not name them.
    $labInstall=('D:\Battle'+'Net\Hearthstone'); $labAccount=('C:\Us'+'ers\HSOffline'+'Probe')
    if ($path -ieq $labInstall -or $path -ilike ($labInstall+'\*') -or $path -ilike ($labAccount+'*')) { throw 'Protected lab location refused' }
}
Assert-Regular $LoaderRoot
# The recorded list (sources, versions, licences, hashes): the only authority for loader files.
$loaderList=Get-Content -LiteralPath "$PSScriptRoot\loader-inputs.json" -Raw | ConvertFrom-Json
if ($loaderList.schema -ne 1) { throw 'loader-inputs.json has an unknown schema' }
$listed=@{}
foreach($entry in $loaderList.files) { $listed[$entry.path]=$entry.sha256.ToUpperInvariant() }
$present=@{}
foreach($file in Get-ChildItem -LiteralPath $LoaderRoot -File -Recurse -Force | Where-Object { $_.Name -ne '.solostone-loader' }) {
    Assert-Regular $file.FullName
    $present[$file.FullName.Substring($LoaderRoot.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
foreach($path in $listed.Keys) { if (-not $present.ContainsKey($path)) { throw "Loader file missing from the folder: $path" } elseif ($present[$path] -cne $listed[$path]) { throw "Loader file differs from loader-inputs.json: $path" } }
foreach($path in $present.Keys) { if (-not $listed.ContainsKey($path)) { throw "Loader folder holds an unlisted file: $path" } }
$DoorstopVersion=$loaderList.doorstopVersion
if (Test-Path -LiteralPath $out) {
    Assert-Regular $out
    if (-not (Test-Path -LiteralPath (Join-Path $out '.solostone-build'))) { throw 'Refusing to replace unmarked build output' }
    Get-ChildItem -LiteralPath $out -Recurse -Force | ForEach-Object { Assert-Regular $_.FullName }
    Remove-Item -LiteralPath $out -Recurse -Force
}
New-Item -ItemType Directory -Path $app -Force | Out-Null
Set-Content -LiteralPath (Join-Path $out '.solostone-build') -Value 'installer build' -Encoding ascii

# The licence texts every download carries, and where they sit in the app folder. check-release requires the same set.
$licenceFiles=@('NOTICE','LICENSE','LICENSE-ADDITIONAL-PERMISSION.md','SOURCE.txt','LICENSES\AGPL-3.0-or-later.txt','LICENSES\CC-BY-4.0.txt','LICENSES\LGPL-2.1-only.txt','LICENSES\MIT.txt')
function Copy-Launcher([string]$From) {
    # No debug symbols: they carry the build machine's paths.
    foreach($file in Get-ChildItem -LiteralPath $From -File | Where-Object { $_.Extension -ne '.pdb' }) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $app $file.Name) }
}
$revision=$null
if ($Staged) {
    # Staged on another machine: the launcher and plugin are already built. Only the loader comes from here.
    $Staged=[IO.Path]::GetFullPath($Staged).TrimEnd('\')
    foreach($needed in 'launcher\SoloStone.exe','plugin\SoloStone.Plugin.dll','plugin\SabberStoneCore.dll','release.json','NOTICE.txt') {
        if (-not (Test-Path -LiteralPath (Join-Path $Staged $needed))) { throw "Staged folder is missing $needed" }
    }
    Copy-Launcher (Join-Path $Staged 'launcher')
    Copy-Item -LiteralPath (Join-Path $Staged 'release.json') -Destination (Join-Path $app 'release.json')
    Copy-Item -LiteralPath (Join-Path $Staged 'NOTICE.txt') -Destination (Join-Path $app 'NOTICE.txt')
    # The licence texts and the source pointer. A live-test build (the lab's own staging) may not have them; a release must.
    $legal=Join-Path $Staged 'legal'
    if (Test-Path -LiteralPath $legal) {
        Assert-Regular $legal
        Copy-Item -Path (Join-Path $legal '*') -Destination $app -Recurse
    } elseif (-not $HookBuild) { throw 'Staged folder is missing legal\ (the licence texts and SOURCE.txt)' }
    $plugin=Join-Path $Staged 'plugin\SoloStone.Plugin.dll'
    $engine=Join-Path $Staged 'plugin\SabberStoneCore.dll'
    $release=Get-Content -LiteralPath (Join-Path $app 'release.json') -Raw | ConvertFrom-Json
    if ($release.schema -ne 1 -or $release.version -cne $Version) { throw 'release.json does not match -Version' }
    $revision=$release.sourceRevision
    $saveSchema=[int]$release.saveSchema
} else {
    $referenceInputs=if ($References) { $refRoot=[IO.Path]::GetFullPath($References).TrimEnd('\'); @("-p:GameReferences=$refRoot\game","-p:BepInExReferences=$refRoot\bepinex") } else { @() }
    $buildInputs=@("-p:SoloStoneRootKey=$RootKey","-p:SoloStoneSigningKey=$SigningKey","-p:SoloStoneRepo=$Repo","-p:SoloStoneSeqFloor=$SeqFloor")
    $built="$out\launcher-build"
    Run-Dotnet (@('build',"$PSScriptRoot\SoloStone.Launcher\SoloStone.Launcher.csproj",'-c','Release','-p:NuGetAudit=false','--output',$built)+$buildInputs)
    Copy-Launcher $built
    Remove-Item -LiteralPath $built -Recurse -Force
    Run-Dotnet @('run','--project',"$PSScriptRoot\SoloStone.Launcher.Tests",'-c','Release')
    # Owned source engine, current names; this opt-in plugin accepts only marked installer copies.
    # Debug, as engine\README.md says to ship it: Debug keeps the shipped engine's exception semantics.
    Run-Dotnet (@('build',"$product\plugin\OfflineProbe.csproj",'-c','Debug','-p:EngineSource=true','-p:Installer=true',
        '-p:NuGetAudit=false','--artifacts-path',"$out\plugin")+$referenceInputs)
    $plugins=@(Get-ChildItem -LiteralPath "$out\plugin\bin\OfflineProbe" -Filter SoloStone.Plugin.dll -Recurse)
    if ($plugins.Count -ne 1) { throw 'Expected exactly one installer plugin build' }
    $plugin=$plugins[0].FullName
    # Anything this plugin adds beyond the default build must exist on Unity's Mono.
    Run-Dotnet (@('build',"$product\plugin\OfflineProbe.csproj",'-c','Debug','-p:EngineSource=true','-p:NuGetAudit=false','--artifacts-path',"$out\plugin-default")+$referenceInputs)
    $baseline=@(Get-ChildItem -LiteralPath "$out\plugin-default\bin\OfflineProbe" -Filter HSNativeUIProbe.dll -Recurse)
    if ($baseline.Count -ne 1) { throw 'Expected exactly one default plugin build' }
    Run-Dotnet @('run','--project',"$PSScriptRoot\SoloStone.Launcher.Tests",'-c','Release','--','tool','mono-guard',$plugin,$baseline[0].FullName,"$PSScriptRoot\mono-assumed.txt",$(if ($References) { $refRoot } else { "$product\references" }))
    $engine=Join-Path (Split-Path $plugin) 'SabberStoneCore.dll'
    $revision=(& git -C $product rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision' }
    # A dirty tree is recorded as such instead of claiming a release tag.
    $dirty=@(& git -C $product status --porcelain -- .).Count -gt 0
    if($LASTEXITCODE -ne 0) { throw 'Cannot identify source state' }
    if ($dirty) { $revision+='-dirty' }
    $profileSource=Get-Content -LiteralPath "$product\plugin\LocalProfile.cs" -Raw
    $match=[regex]::Match($profileSource,'private const int CurrentVersion = (\d+);')
    if (-not $match.Success) { throw 'Cannot read the save schema from LocalProfile.cs' }
    $saveSchema=[int]$match.Groups[1].Value
    @{schema=1; version=$Version; saveSchema=$saveSchema; sourceRevision=$revision} | ConvertTo-Json | Set-Content -LiteralPath "$app\release.json" -Encoding ascii
    Copy-Item -LiteralPath "$PSScriptRoot\NOTICE.txt" -Destination "$app\NOTICE.txt"
    # Licence texts from the repository root. Same files stage.py copies.
    $legalRoot=$product
    if (-not ((Test-Path -LiteralPath "$legalRoot\NOTICE") -and (Test-Path -LiteralPath "$legalRoot\LICENSES"))) { throw 'Cannot find the licence files (NOTICE, LICENSE, LICENSES\)' }
    foreach($name in 'NOTICE','LICENSE','LICENSE-ADDITIONAL-PERMISSION.md') { Copy-Item -LiteralPath "$legalRoot\$name" -Destination "$app\$name" }
    Copy-Item -LiteralPath "$legalRoot\LICENSES" -Destination "$app\LICENSES" -Recurse
    $sourceLine=if ($Repo) { "https://github.com/$Repo" } else { '(the repository address was not set when this was built)' }
    (Get-Content -LiteralPath "$PSScriptRoot\SOURCE.template.txt" -Raw).Replace('@VERSION@',$Version).Replace('@SOURCE_LINE@',$sourceLine).Replace('@REVISION@',$revision) |
        Set-Content -LiteralPath "$app\SOURCE.txt" -Encoding ascii
}
if (-not $HookBuild) {
    foreach($name in $licenceFiles) { if (-not (Test-Path -LiteralPath (Join-Path $app $name))) { throw "The app folder has no $name. Every download carries the licence texts and SOURCE.txt." } }
}
$payload=Join-Path $app 'payload'
New-Item -ItemType Directory -Path "$payload\BepInEx\core","$payload\BepInEx\plugins" -Force | Out-Null
# Never copy source plugins, patchers, configuration, caches, or another mod's loader config.
# The client's Managed folder is stripped: BepInEx's preloader and the plugin need the full Mono library set (BepInEx\unstripped_corlib, Unity 6000.3.11, loader-inputs.json) first on the search path.
# Copy exactly the listed files, then confirm the payload holds nothing else from the loader and every file has its listed hash.
foreach($path in $listed.Keys) {
    $target=Join-Path $payload ($path -replace '/','\')
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Assert-Regular "$LoaderRoot\$($path -replace '/','\')"
    Copy-Item -LiteralPath "$LoaderRoot\$($path -replace '/','\')" -Destination $target
}
Copy-Item -LiteralPath $plugin -Destination "$payload\BepInEx\plugins\SoloStone.Plugin.dll"
Copy-Item -LiteralPath $engine -Destination "$payload\BepInEx\plugins\SabberStoneCore.dll"
$loaderCopied=@(Get-ChildItem -LiteralPath $payload -File -Recurse -Force | ForEach-Object { $_.FullName.Substring($payload.Length+1).Replace('\','/') } |
    Where-Object { $_ -eq 'winhttp.dll' -or $_ -like 'BepInEx/core/*' -or $_ -like 'BepInEx/unstripped_corlib/*' })
if ($loaderCopied.Count -ne $listed.Count) { throw "Payload loader files ($($loaderCopied.Count)) differ from loader-inputs.json ($($listed.Count))" }
foreach($path in $listed.Keys) { Pin (Join-Path $payload ($path -replace '/','\')) $listed[$path] }
Copy-Item -LiteralPath "$product\client.config" -Destination "$payload\client.config"
@'
[General]
enabled=true
target_assembly=BepInEx\core\BepInEx.Preloader.dll
redirect_output_log=false
ignore_disable_switch=false

[UnityMono]
dll_search_path_override=BepInEx\unstripped_corlib
'@ | Set-Content -LiteralPath "$payload\doorstop_config.ini" -Encoding ascii
$pins=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName) {
    $relative=$file.FullName.Substring($payload.Length+1).Replace('\','/')
    $pins[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
$bepVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo("$payload\BepInEx\core\BepInEx.dll").FileVersion
if (-not $bepVersion.StartsWith($loaderList.bepInExVersion)) { throw "BepInEx.dll says $bepVersion, loader-inputs.json says $($loaderList.bepInExVersion)" }
@{schema=1; bepInExVersion=$bepVersion; doorstopVersion=$DoorstopVersion; sourceRevision=$revision;
    engineSha256=$pins['BepInEx/plugins/SabberStoneCore.dll']; pluginSha256=$pins['BepInEx/plugins/SoloStone.Plugin.dll']; files=$pins
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$app\payload.json" -Encoding ascii

# The release rules, run by the launcher itself: no Hearthstone client file, no stray program, no hook in a release.
# Limit: a hash compare catches only this reference client's bytes. A file from a different client build can't be caught this way, so provenance stays open.
$ClientReference=[IO.Path]::GetFullPath($ClientReference).TrimEnd('\')
$checkArgs=@('check-payload','--app',"`"$app`"",'--client-reference',"`"$ClientReference`"",'--quiet')
if ($HookBuild) { $checkArgs+='--hook-build' }
$check=Start-Process -FilePath "$app\SoloStone.exe" -ArgumentList $checkArgs -Wait -PassThru -NoNewWindow
if ($check.ExitCode -ne 0) { throw "Payload check failed (exit $($check.ExitCode)). Nothing was built." }

# The update zip is the app folder, with forward-slash names and nothing the installer manages itself.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# A hook build's files say so in their names. Its zip and setup can't be mistaken for a release, and check-release refuses them by name and by content.
$artifactBase=if ($HookBuild) { 'SoloStone-TEST-HOOKS' } else { 'SoloStone' }
$zipPath=Join-Path $out "$artifactBase-$Version.zip"
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($file in Get-ChildItem -LiteralPath $app -File -Recurse | Sort-Object FullName) {
        $relative=$file.FullName.Substring($app.Length+1).Replace('\','/')
        if ($file.Name -like 'unins*' -or $file.Name -eq '.solostone-app') { continue }
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose() }
$zipItem=Get-Item -LiteralPath $zipPath
$result=[ordered]@{hookBuild=[bool]$HookBuild; version=$Version; saveSchema=$saveSchema; sourceRevision=$revision; zip=[ordered]@{name=$zipItem.Name; size=$zipItem.Length; sha256=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash}}
if (-not $NoSetup) {
    if (-not $Iscc) {
        Run-Dotnet @('restore',"$PSScriptRoot\InnoSetup.proj",'--packages',"$PSScriptRoot\packages",'-p:NuGetAudit=false')
        $found=@(Get-ChildItem -LiteralPath "$PSScriptRoot\packages\tools.innosetup\6.4.3" -Filter ISCC.exe -Recurse)
        if($found.Count -ne 1) { throw 'Tools.InnoSetup must contain exactly one ISCC.exe' }
        $Iscc=$found[0].FullName
    }
    $setupBase=if ($HookBuild) { "SoloStone-TEST-HOOKS-Setup-$Version" } else { "SoloStone-Setup-$Version" }
    & $Iscc "/DAppVersion=$Version" "/DStageDir=$app" "/DSetupBase=$setupBase" "/O$out" "$PSScriptRoot\SoloStone.iss"
    if($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }
    $setup=Get-Item -LiteralPath "$out\$setupBase.exe"
    $result.setup=[ordered]@{name=$setup.Name; size=$setup.Length; sha256=(Get-FileHash -LiteralPath $setup.FullName -Algorithm SHA256).Hash}
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$out\build-result.json" -Encoding ascii
# Every release build ends here, and nothing may be published without it: no hook trace in launcher or plugin, the right names, a NOTICE.
if (-not $HookBuild) {
    $releaseArgs=@('check-release','--out',"`"$out`"",'--version',$Version,'--quiet')
    if ($NoSetup) { $releaseArgs+='--no-setup' }
    $released=Start-Process -FilePath "$app\SoloStone.exe" -ArgumentList $releaseArgs -Wait -PassThru -NoNewWindow
    if ($released.ExitCode -ne 0) { throw "Release check failed (exit $($released.ExitCode)). Do not publish this build." }
}
Write-Output "Built $out (version $Version, save schema $saveSchema)"
