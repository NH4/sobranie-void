<#
.SYNOPSIS
  Regenerates src/Sobranie.Orchestrator/seed-data.json from the real
  sobranie.mk MP roster + scripts/personas/*.md persona files.

.DESCRIPTION
  Source of truth: sobranie.mk MakePostRequest/GetParliamentMPsNoImage
  (see scripts/mps-with-parties.slim.json for the raw response and
  scripts/mps-with-parties.slim.json for the distilled record set).

  - Maps every distinct Cyrillic party name to a stable short PartyId
    slug and a brand-aware color hex (hand-curated; see $PartySlugMap).
  - Tags 7 MainCast MPs by UserId; every other MP is CastTier.Chorus.
  - Reads scripts/personas/*.md files for MainCast persona content and
    trait overrides; chorus lines come from $ChorusTemplates.
  - Emits a chorus line per party so the Chorus tier has something to
    vocalize regardless of party size.
  - Preserves the SeedDocument schema consumed by SobranieDataSeeder.

  Run after updating mps-with-parties.slim.json or persona files.
  The seeder bails out if the Parties table is non-empty, so delete
  the SQLite file first if you want the new data to take effect.
#>
[CmdletBinding()]
param(
  [string]$SlimPath     = 'C:\Users\Van4o\sobranie-void\scripts\mps-with-parties.slim.json',
  [string]$OutPath      = 'C:\Users\Van4o\sobranie-void\src\Sobranie.Orchestrator\seed-data.json',
  [string]$PersonasDir  = 'C:\Users\Van4o\sobranie-void\scripts\personas'
)

Add-Type -AssemblyName System.Web.Extensions
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue

$raw = [IO.File]::ReadAllText($SlimPath, [Text.Encoding]::UTF8)
$mps = $serializer.DeserializeObject($raw)

# Cyrillic party display name -> (PartyId slug, Short, Color).
# Colors are hand-picked approximations of each party's public brand.
$PartySlugMap = @{
  'ВМРО-ДПМНЕ'                                               = @('vmro_dpmne', 'ВМРО',   '#C8102E')
  'Социјалдемократски сојуз на Македонија'                   = @('sdsm',       'СДСМ',   '#E63946')
  'Движење БЕСА'                                             = @('besa',       'БЕСА',   '#00A878')
  'Демократска унија за интеграција'                         = @('dui',        'ДУИ',    '#F2C94C')
  'Алијанса за Албанците'                                    = @('alijansa',   'АА',     '#1E90FF')
  'Движење ЗНАМ'                                             = @('znam',       'ЗНАМ',   '#8E44AD')
  'Левица'                                                   = @('levica',     'Левица', '#B71C1C')
  'Независни пратеници'                                      = @('nezavisni',  'Незав.', '#7F8C8D')
  'Социјалистичка партија на Македонија'                     = @('spm',        'СПМ',    '#D35400')
  'Нова социјалдемократска партија'                          = @('nsdp',       'НСДП',   '#F08080')
  'Либерално-демократска партија'                            = @('ldp',        'ЛДП',    '#F4D03F')
  'Движење на Турците на Македонија за правда и демократија' = @('dtm',        'ДТМ',    '#C0392B')
  'Движење „Попули“'                                         = @('populi',     'Попули', '#16A085')
  'Демократска партија на Србите'                            = @('dps',        'ДПС',    '#2C3E50')
  'Алтернатива'                                              = @('alternativa','Алт.',   '#27AE60')
  'Демократска партија на Албанците'                         = @('dpa',        'ДПА',    '#3498DB')
}

# Short generic chorus lines per party, tagged by FSM event type.
$DefaultChorusTags = @('general_support', 'heckle_opposition', 'general_neutral')

# Seven sitting MPs selected as MainCast per docs/decisions.md D-017
# (as corrected in D-018).
# Christian Mickoski is excluded - he is Prime Minister and his MP
# mandate is currently suspended, so he is not a member of this session.
# Talat Xhaferi is former Speaker (2017-2024); Afrim Gashi is the
# current Speaker since 28 May 2024.
$MainCastUserIds = @(
  'a47a3a15-9f13-4ba6-afd0-53db79fc2f02',  # Venko Filipche (SDSM)
  '4eeadcc4-4b7b-4708-ae9b-1ebf1c0a25f9',  # Dimitar Apasiev (Levica)
  'f66f1d74-95ad-4df7-8586-17bfdb169228',  # Antonijo Miloshoski (VMRO-DPMNE, Deputy Speaker)
  'e1ead9d2-7a0e-4478-9fc7-34581e2937ee',  # Talat Xhaferi (DUI, former Speaker 2017-2024, now opposition MP)
  '941c5a06-34cb-4cb1-955a-25344a8f3a48',  # Ali Ahmeti (DUI founder)
  'b0688e81-82f8-4d41-bcfd-23ed8d9a27d9',  # Amar Mecinovikj (Levica)
  '844cc6b4-6299-4f8e-bc99-daa48cb23a23'   # Afrim Gashi (Alternativa / VLEN, Speaker since 28 May 2024)
)

# Upstream sobranie.mk leaves Coalition blank for all Alternativa rows,
# but Alternativa ran inside VLEN in May 2024 and Gashi's Speakership
# is a VLEN-coalition appointment. Targeted override for Gashi only.
$CoalitionOverrides = @{
  '844cc6b4-6299-4f8e-bc99-daa48cb23a23' = 'ВЛЕН'
}

# Short generic chorus lines per party
$ChorusTemplates = @(
  @{ tag = 'general_support';   text = 'Така е!' },
  @{ tag = 'general_support';   text = 'Точно!' },
  @{ tag = 'heckle_opposition'; text = 'Срам!' },
  @{ tag = 'heckle_opposition'; text = 'Лаги!' },
  @{ tag = 'general_neutral';   text = 'Гледаме, гледаме.' }
)

# --- Persona file parser ---------------------------------------------------

function Read-PersonaFile {
  param([string]$Path)

  $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)

  # Extract display name from title line: "# Име Презиме — Име Презиме"
  $displayName = $null
  $lines = $text -split "`n"
  foreach ($l in $lines) {
    if ($l -match '^\s*#\s+.+?\s+—\s+(.+)$') {
      $displayName = $matches[1].Trim()
      break
    }
  }

  if (-not $displayName) {
    throw ("Could not parse display name from persona file: {0}" -f $Path)
  }

  # Pull out sections; content runs until the next ## or end of file.
  $sections = @{}

  $currentSection = $null
  $currentContent = [System.Collections.Generic.List[string]]::new()

  foreach ($l in $lines) {
    # Skip comment lines (// style)
    if ($l -match '^\s*//') { continue }

    if ($l -match '^\s*##\s+(\S[^\r\n]*)') {
      if ($currentSection) {
        $sections[$currentSection] = ($currentContent -join "`n").Trim()
      }
      $currentSection = $matches[1].Trim()
      $currentContent.Clear()
    }
    elseif ($currentSection) {
      $currentContent.Add($l)
    }
  }
  if ($currentSection) {
    $sections[$currentSection] = ($currentContent -join "`n").Trim()
  }

  # Parse Traits section: "Aggression: 0.8", etc.
  $traits = @{ aggression = 0.5; legalism = 0.5; populism = 0.5 }
  if ($sections['Traits']) {
    foreach ($l in ($sections['Traits'] -split "`n")) {
      if ($l -match '^\s*(Aggression|Legalism|Populism)\s*:\s*([\d.]+)') {
        $key = $matches[1].ToLower()
        $traits[$key] = [double]$matches[2]
      }
    }
  }

  # Parse SignatureMoves section: "- ""signature text"""
  $sigMoves = [System.Collections.Generic.List[object]]::new()
  if ($sections['SignatureMoves']) {
    $inList = $false
    foreach ($l in ($sections['SignatureMoves'] -split "`n")) {
      if ($l -match '^\s*-\s+["""](.+)["""]\s*$') {
        $sigMoves.Add([PSCustomObject]@{
          label         = ''
          exemplar     = $matches[1]
          triggerWeight = 1.0
        })
      }
    }
  }

  return @{
    DisplayName    = $displayName
    PersonaCore    = $sections['Core']
    OverlayGentle  = $sections['OverlayGentle']
    OverlaySharp    = $sections['OverlaySharp']
    OverlayAbsurd   = $sections['OverlayAbsurd']
    Traits         = $traits
    SignatureMoves = $sigMoves
  }
}

# --- Load all persona files -----------------------------------------------

$personasByName = @{}
if (Test-Path $PersonasDir) {
  foreach ($f in Get-ChildItem -Path $PersonasDir -Filter '*.md') {
    $p = Read-PersonaFile -Path $f.FullName
    $personasByName[$p.DisplayName] = $p
    Write-Output ("Loaded persona: {0}" -f $p.DisplayName)
  }
} else {
  Write-Warning ("Personas directory not found: {0}" -f $PersonasDir)
}

# --- Build parties --------------------------------------------------------

$partiesByTitle = @{}
foreach ($m in $mps) {
  $title = [string]$m['PoliticalParty']
  if (-not $partiesByTitle.ContainsKey($title)) { $partiesByTitle[$title] = 0 }
  $partiesByTitle[$title] = $partiesByTitle[$title] + 1
}

$partyOut = New-Object System.Collections.Generic.List[object]
$unknownParties = New-Object System.Collections.Generic.List[string]
foreach ($title in $partiesByTitle.Keys) {
  if ($PartySlugMap.ContainsKey($title)) {
    $slug,$short,$color = $PartySlugMap[$title]
  } else {
    $slug  = '_unknown_' + ($partyOut.Count.ToString())
    $short = '???'
    $color = '#95A5A6'
    $unknownParties.Add($title) | Out-Null
  }
  $partyOut.Add([PSCustomObject]@{
    partyId     = $slug
    displayName = $title
    shortName   = $short
    colorHex    = $color
    seatCount   = $partiesByTitle[$title]
  })
}

if ($unknownParties.Count -gt 0) {
  Write-Warning ("Unmapped parties (given _unknown_* slug): {0}" -f ($unknownParties -join '; '))
}

# Title -> slug map now that every party has one
$titleToSlug = @{}
foreach ($p in $partyOut) { $titleToSlug[$p.displayName] = $p.partyId }

# --- Build MPs ------------------------------------------------------------

$mainCastLookup = @{}
foreach ($id in $MainCastUserIds) { $mainCastLookup[$id] = $true }

$mpOut = New-Object System.Collections.Generic.List[object]
$seatIndex = 0
$missingMainCast = New-Object System.Collections.Generic.List[string]
foreach ($id in $MainCastUserIds) {
  $hit = $mps | Where-Object { $_['UserId'] -eq $id }
  if (-not $hit) { $missingMainCast.Add($id) | Out-Null }
}
if ($missingMainCast.Count -gt 0) {
  throw ("MainCast UserIds not found in slim roster: {0}" -f ($missingMainCast -join ', '))
}

# Stable ordering: MainCast first, then by surname
$sortedMps = $mps | Sort-Object -Property @{
  Expression = { if ($mainCastLookup.ContainsKey($_['UserId'])) { 0 } else { 1 } }
}, @{
  Expression = { [string]$_['FullName'] }
}

foreach ($m in $sortedMps) {
  $uid   = [string]$m['UserId']
  $title = [string]$m['PoliticalParty']
  $tier  = if ($mainCastLookup.ContainsKey($uid)) { 'MainCast' } else { 'Chorus' }
  $coal  = [string]$m['Coalition']
  if ([string]::IsNullOrWhiteSpace($coal)) { $coal = $null }
  if ($CoalitionOverrides.ContainsKey($uid)) { $coal = $CoalitionOverrides[$uid] }

  $fullName = [string]$m['FullName']

  # Check for persona override by display name
  $persona = $null
  if ($personasByName.ContainsKey($fullName)) {
    $persona = $personasByName[$fullName]
  }

  if ($persona) {
    $aggression = $persona.Traits['aggression']
    $legalism   = $persona.Traits['legalism']
    $populism   = $persona.Traits['populism']
  } else {
    $aggression = 0.5
    $legalism   = 0.5
    $populism   = 0.5
  }

  $mpOut.Add([PSCustomObject]@{
    mpId                 = $uid
    partyId              = $titleToSlug[$title]
    displayName          = $fullName
    coalition            = $coal
    tier                 = $tier
    aggression           = $aggression
    legalism             = $legalism
    populism             = $populism
    seatIndex            = $seatIndex
    personaCore          = if ($persona) { $persona.PersonaCore } else { $null }
    personaOverlayGentle = if ($persona) { $persona.OverlayGentle } else { $null }
    personaOverlaySharp  = if ($persona) { $persona.OverlaySharp } else { $null }
    personaOverlayAbsurd = if ($persona) { $persona.OverlayAbsurd } else { $null }
    signatureMoves       = if ($persona) { $persona.SignatureMoves } else { @() }
  })
  $seatIndex++
}

# --- Build chorus lines --------------------------------------------------

$chorusOut = New-Object System.Collections.Generic.List[object]
foreach ($p in $partyOut) {
  foreach ($tpl in $ChorusTemplates) {
    $chorusOut.Add([PSCustomObject]@{
      partyId  = $p.partyId
      topicTag = $tpl.tag
      text     = $tpl.text
      weight   = 1.0
    })
  }
}

# --- Emit -----------------------------------------------------------------

$doc = [ordered]@{
  '_comment'   = 'Generated by scripts/Generate-SeedData.ps1 from scripts/mps-with-parties.slim.json + scripts/personas/*. See docs/decisions.md D-017.'
  '_schema'    = 'v3'
  parties      = $partyOut
  mps          = $mpOut
  chorusLines  = $chorusOut
}

# ConvertTo-Json in PS 5.1 caps depth at 2 by default; passing -Depth fixes it.
$json = $doc | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($OutPath, $json, [Text.UTF8Encoding]::new($false))

Write-Output ("Wrote {0}" -f $OutPath)
Write-Output ("  parties  : {0}" -f $partyOut.Count)
Write-Output ("  mps      : {0} ({1} MainCast)" -f $mpOut.Count, $MainCastUserIds.Count)
Write-Output ("  chorus   : {0}" -f $chorusOut.Count)
Write-Output ("  personas : {0}" -f $personasByName.Count)
