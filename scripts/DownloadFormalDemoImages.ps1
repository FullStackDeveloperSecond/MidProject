param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\MidProject\FormalDemoAssets'),
    [int]$ImageCount = 120
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$resolvedProjectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$expectedOutput = [System.IO.Path]::GetFullPath(
    (Join-Path $resolvedProjectRoot 'MidProject\FormalDemoAssets'))

if (-not [string]::Equals(
    $resolvedOutput.TrimEnd('\'),
    $expectedOutput.TrimEnd('\'),
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to replace unexpected output directory: $resolvedOutput"
}
if ($ImageCount -ne 120) {
    throw 'The formal presentation dataset requires exactly 120 source images.'
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
Get-ChildItem -LiteralPath $resolvedOutput -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'MidProjectFormalDemoOpenImages'
if (Test-Path -LiteralPath $temporaryRoot) {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

$bboxFile = Join-Path $temporaryRoot 'validation-annotations-bbox.csv'
$metadataFile = Join-Path $temporaryRoot 'validation-images-with-rotation.csv'
$userAgent = 'MidProjectFormalDemoData/1.0 (https://github.com/FullStackDeveloperSecond/MidProject; educational presentation data)'
$foodLabels = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        '/m/01_bhs', # Fast food
        '/m/0270h',  # Dessert
        '/m/0271t',  # Drink
        '/m/02vqfm', # Coffee
        '/m/02wbm',  # Food
        '/m/02xwb',  # Fruit
        '/m/052lwg6',# Baked goods
        '/m/05z55',  # Pasta
        '/m/0663v',  # Pizza
        '/m/07030',  # Sushi
        '/m/09728',  # Bread
        '/m/0f4s2w', # Vegetable
        '/m/0fszt',  # Cake
        '/m/0grw1',  # Salad
        '/m/0l515'   # Sandwich
    ),
    [System.StringComparer]::Ordinal)

try {
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri 'https://storage.googleapis.com/openimages/v5/validation-annotations-bbox.csv' `
        -Headers @{ 'User-Agent' = $userAgent } `
        -TimeoutSec 120 `
        -OutFile $bboxFile
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri 'https://storage.googleapis.com/openimages/2018_04/validation/validation-images-with-rotation.csv' `
        -Headers @{ 'User-Agent' = $userAgent } `
        -TimeoutSec 120 `
        -OutFile $metadataFile

    $candidateIds = Import-Csv -LiteralPath $bboxFile |
        Where-Object { $foodLabels.Contains([string]$_.LabelName) } |
        Select-Object -ExpandProperty ImageID -Unique -First 1000
    $candidateIdSet = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$candidateIds,
        [System.StringComparer]::OrdinalIgnoreCase)

    $metadata = Import-Csv -LiteralPath $metadataFile |
        Where-Object {
            $candidateIdSet.Contains([string]$_.ImageID) -and
            $_.License -match '^https?://creativecommons\.org/licenses/by/2\.0/?$'
        } |
        Sort-Object ImageID
    if (@($metadata).Count -lt $ImageCount) {
        throw "Open Images returned only $(@($metadata).Count) food images with CC BY 2.0 metadata."
    }

    Add-Type -AssemblyName System.Net.Http
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AutomaticDecompression =
        [System.Net.DecompressionMethods]::GZip -bor
        [System.Net.DecompressionMethods]::Deflate
    $httpClient = [System.Net.Http.HttpClient]::new($handler)
    $httpClient.Timeout = [TimeSpan]::FromSeconds(45)
    $httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($userAgent)

    $attributions = [System.Collections.Generic.List[object]]::new()
    $downloaded = 0
    try {
        for ($start = 0; $start -lt $metadata.Count -and $downloaded -lt $ImageCount; $start += 8) {
            $batch = @($metadata | Select-Object -Skip $start -First 8)
            $downloadUrls = @($batch | ForEach-Object {
                "https://open-images-dataset.s3.amazonaws.com/validation/$($_.ImageID).jpg"
            })
            $tasks = @($downloadUrls | ForEach-Object {
                $httpClient.GetByteArrayAsync($_)
            })

            try {
                [void][System.Threading.Tasks.Task]::WaitAll(
                    [System.Threading.Tasks.Task[]]$tasks,
                    [TimeSpan]::FromSeconds(50))
            }
            catch {
                # Individual failures are skipped; later candidates fill the quota.
            }

            for ($batchIndex = 0; $batchIndex -lt $batch.Count -and $downloaded -lt $ImageCount; $batchIndex++) {
                $task = $tasks[$batchIndex]
                if ($task.Status -ne [System.Threading.Tasks.TaskStatus]::RanToCompletion) {
                    continue
                }

                $bytes = $task.Result
                if (-not $bytes -or $bytes.Length -lt 10KB) {
                    continue
                }

                $downloaded++
                $fileName = "formal-source-{0:D3}.jpg" -f $downloaded
                [System.IO.File]::WriteAllBytes(
                    (Join-Path $resolvedOutput $fileName),
                    $bytes)
                $item = $batch[$batchIndex]
                $attributions.Add([pscustomobject]@{
                    File = $fileName
                    OpenImagesId = [string]$item.ImageID
                    Title = [string]$item.Title
                    Creator = [string]$item.Author
                    CreatorProfile = [string]$item.AuthorProfileURL
                    License = 'CC BY 2.0'
                    LicenseUrl = [string]$item.License
                    SourcePage = [string]$item.OriginalLandingURL
                    OriginalUrl = [string]$item.OriginalURL
                    DatasetUrl = $downloadUrls[$batchIndex]
                })
                Write-Output "Downloaded $downloaded/$ImageCount"
            }
        }
    }
    finally {
        $httpClient.Dispose()
        $handler.Dispose()
    }

    if ($downloaded -ne $ImageCount) {
        throw "Only downloaded $downloaded of $ImageCount required Open Images food images."
    }

    $attributions |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $resolvedOutput 'ATTRIBUTIONS.json') -Encoding UTF8

    $markdown = [System.Collections.Generic.List[string]]::new()
    $markdown.Add('# Formal Demo Image Attributions')
    $markdown.Add('')
    $markdown.Add('These food and drink presentation images come from the Open Images validation set.')
    $markdown.Add('Every selected metadata row identifies the image as CC BY 2.0. Creator, original source, and license links are retained below.')
    $markdown.Add('')
    foreach ($item in $attributions) {
        $safeTitle = ([string]$item.Title).Replace('|', '\|')
        $safeCreator = ([string]$item.Creator).Replace('|', '\|')
        $markdown.Add(
            "- **$($item.File)** — $safeTitle; creator: $safeCreator; " +
            "license: [CC BY 2.0]($($item.LicenseUrl)); " +
            "[source]($($item.SourcePage))")
    }
    $markdown |
        Set-Content -LiteralPath (Join-Path $resolvedOutput 'ATTRIBUTIONS.md') -Encoding UTF8

    Write-Output "Completed: $downloaded CC BY 2.0 food images in $resolvedOutput"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
