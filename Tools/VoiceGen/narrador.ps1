# The arena's welcome (José, 2026-11-02), from the Windows voice "Microsoft Daniel" (pt-BR): much
# deeper and a bit faster, with a touch of hall. The multi-kill lines wait for recorded audio.
#   powershell -ExecutionPolicy Bypass -File Tools/VoiceGen/narrador.ps1 <folder for the .ogg files>
# Needs ffmpeg, and Windows PowerShell 5.1 for the WinRT call.
param([Parameter(Mandatory = $true)][string]$Out)

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
} | Select-Object -First 1

function Await($operation, [Type]$type) {
    $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
    $task.Wait() | Out-Null
    $task.Result
}

$lines = [ordered]@{
    narrator_welcome = 'Bem-vindo à arena.'
}

$voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -match 'Daniel' } | Select-Object -First 1
if (-not $voice) { throw 'Microsoft Daniel (pt-BR) is not installed' }
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$synth.Voice = $voice

New-Item -ItemType Directory -Force $Out | Out-Null
# (the voice's own sample rate, so the pitch really goes down)
function Filter-For([int]$rate) {
    'silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,' +
    "asetrate=$([int]($rate * 0.72)),aresample=44100,atempo=1.6," +
    'aecho=0.9:0.7:60|140:0.18|0.1,' +
    'highpass=f=70,lowpass=f=7000,acompressor=threshold=0.1:ratio=4:attack=5:release=120,' +
    'loudnorm=I=-14:TP=-1.5:LRA=7'
}

foreach ($name in $lines.Keys) {
    $ssml = '<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="pt-BR"><prosody pitch="-10%">' + $lines[$name] + '</prosody></speak>'
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $null = Await ($reader.LoadAsync([uint32]$stream.Size)) ([uint32])
    $bytes = New-Object byte[] ([int]$stream.Size)
    $reader.ReadBytes($bytes)
    $wav = Join-Path $env:TEMP "$name.wav"
    [IO.File]::WriteAllBytes($wav, $bytes)
    $rate = & ffprobe -v error -select_streams a -show_entries stream=sample_rate -of csv=p=0 $wav
    & ffmpeg -y -loglevel error -i $wav -af (Filter-For $rate) -ar 44100 -ac 1 -c:a libvorbis -q:a 5 (Join-Path $Out "$name.ogg")
    Remove-Item $wav
    Write-Output "$name ($rate Hz)"
}
