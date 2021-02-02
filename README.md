# MinimalAudioWaveformGenerator
<p align="centre">
<b>A small console-application that generates simple-looking audio waveforms, perfect for a minimal aesthetic.</b>
</p>

This projects is using [Un4seen's BASS library](http://www.un4seen.com/).

## Prerequisites

Windows 64-Bit

[.NET 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)

## Examples
Here are some examples using different parameter values:

``` csharp
BlockSize = 1
SpaceSize = 0
ImageWidth = 1200
PeakHeight = 128
AverageScale = 16
```

<p align="center">
  <img src="assets/1, 0, 1200, 128, 16.png">
</p>

``` csharp
BlockSize = 4
SpaceSize = 2
ImageWidth = 1200
PeakHeight = 128
AverageScale = 16
```

<p align="center">
  <img src="assets/4, 2, 1200, 128, 16.png">
</p>

``` csharp
BlockSize = 8
SpaceSize = 2
ImageWidth = 1200
PeakHeight = 128
AverageScale = 16
```

<p align="center">
  <img src="assets/8, 2, 1200, 128, 16.png">
</p>

``` csharp
BlockSize = 8
SpaceSize = 8
ImageWidth = 1200
PeakHeight = 128
AverageScale = 16
```

<p align="center">
  <img src="assets/8, 8, 1200, 128, 16.png">
</p>

``` csharp
BlockSize = 16
SpaceSize = 0
ImageWidth = 1200
PeakHeight = 128
AverageScale = 16
```

<p align="center">
  <img src="assets/16, 0, 1200, 128, 16.png">
</p>
