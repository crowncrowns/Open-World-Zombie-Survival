$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot '..\..\work\PostApocalypticFurniture\UnityProject\Assets\PostApocalypticFurniture\Audio\Radio'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$voice=New-Object -ComObject SAPI.SpVoice
$ru=$voice.GetVoices() | Where-Object { $_.GetDescription() -match 'Irina' } | Select-Object -First 1
if ($null -eq $ru) { throw 'Russian speech voice is unavailable' }
$voice.Voice=$ru
$voice.Rate=-1
$messages=@{
  'OriginalAnnouncer.wav'='Добрый вечер. В эфире тихая волна. За окнами осень. Если вы слышите эту передачу, настройте приёмник и берегите батарейки. Музыка из старого дома продолжается.'
  'SurvivorMessage.wav'='Говорит станция Север. Мы остаёмся на прежней частоте. Воду необходимо кипятить. Не входите в тёмные подъезды по одному. Встреча у старой водонапорной башни на рассвете. Приём.'
  'EmergencyBroadcast.wav'='Внимание. Экстренное сообщение. Восточный переход закрыт. Двигайтесь только при дневном свете. Повторяю: восточный переход закрыт. Сохраняйте спокойствие. Передача закончена.'
}
foreach ($item in $messages.GetEnumerator()) {
  $stream=New-Object -ComObject SAPI.SpFileStream
  $stream.Format.Type=22
  $stream.Open((Join-Path $root $item.Key),3,$false)
  $voice.AudioOutputStream=$stream
  [void]$voice.Speak($item.Value)
  $stream.Close()
}
Write-Output 'ORIGINAL_RUSSIAN_SPEECH_PASS'
