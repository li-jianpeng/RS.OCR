param([string]$ModelVersion = "v4", [string]$ModelDir = "models")
$ErrorActionPreference = "Stop"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RS.OCR Model Download" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (-not (Test-Path $ModelDir)) {
    New-Item -ItemType Directory -Path $ModelDir -Force | Out-Null
}

$baseUrl = "https://www.modelscope.cn/models"
$models = @()

if ($ModelVersion -eq "v4") {
    $models = @(
        @{ Name = "det.onnx"; Url = "$baseUrl/RapidAI/RapidOCR/resolve/master/models/ch_PP-OCRv4_det_infer.onnx"; Desc = "PP-OCRv4 detection (~4.6MB)" },
        @{ Name = "rec.onnx"; Url = "$baseUrl/RapidAI/RapidOCR/resolve/master/models/ch_PP-OCRv4_rec_infer.onnx"; Desc = "PP-OCRv4 recognition (~10.4MB)" }
    )
} elseif ($ModelVersion -eq "v5") {
    $models = @(
        @{ Name = "det.onnx"; Url = "$baseUrl/RapidAI/RapidOCR/resolve/master/models/ch_PP-OCRv5_mobile_det_infer.onnx"; Desc = "PP-OCRv5 detection (~4.6MB)" },
        @{ Name = "rec.onnx"; Url = "$baseUrl/RapidAI/RapidOCR/resolve/master/models/ch_PP-OCRv5_mobile_rec_infer.onnx"; Desc = "PP-OCRv5 recognition (~15.8MB)" }
    )
} else {
    Write-Host "[ERROR] Unsupported version: $ModelVersion. Use v4 or v5" -ForegroundColor Red
    exit 1
}

$dictUrl = "$baseUrl/RapidAI/RapidOCR/resolve/master/ppocr_keys_v1.txt"
$dictFile = Join-Path $ModelDir "ppocr_keys_v1.txt"

function Download-File { param([string]$Url, [string]$Output)
    if (Test-Path $Output) { Write-Host "[SKIP] $Output exists" -ForegroundColor Yellow; return $true }
    try {
        Write-Host "[DOWNLOAD] $Url" -ForegroundColor Gray
        (New-Object System.Net.WebClient).DownloadFile($Url, $Output)
        Write-Host "[OK] $Output" -ForegroundColor Green; return $true
    } catch { Write-Host "[FAIL] $_" -ForegroundColor Red; return $false }
}

foreach ($m in $models) {
    Write-Host ">>> $($m.Desc)" -ForegroundColor White
    Download-File -Url $m.Url -Output (Join-Path $ModelDir $m.Name)
}

Write-Host ">>> Dictionary file" -ForegroundColor White
Download-File -Url $dictUrl -Output $dictFile

Write-Host ""
Write-Host "Done. Run: cd src/RS.OCR.WebApi ; dotnet run" -ForegroundColor Yellow