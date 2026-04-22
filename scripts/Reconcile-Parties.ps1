param(
  [string]$InPath = 'C:\Users\Van4o\sobranie-void\scripts\mps-with-parties.slim.json',
  [string]$CkanPath = 'C:\Users\Van4o\sobranie-void\scripts\pratenici-2024-2028.raw.json',
  [string]$OutPath = 'C:\Users\Van4o\sobranie-void\scripts\reconcile.txt'
)

Add-Type -AssemblyName System.Web.Extensions
$s = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$s.MaxJsonLength = [int]::MaxValue
$s.RecursionLimit = 100

$truth = $s.DeserializeObject([IO.File]::ReadAllText($InPath, [Text.Encoding]::UTF8))
$ckan  = $s.DeserializeObject([IO.File]::ReadAllText($CkanPath, [Text.Encoding]::UTF8))

# Build lookups
$truthById = @{}
foreach ($t in $truth) { $truthById[$t['UserId']] = $t }

$ckanById = @{}
foreach ($c in $ckan) { $ckanById[$c['UserId']] = $c }

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("=== Set differences ===")
$lines.Add("In truth but not CKAN: $((@($truthById.Keys | Where-Object { -not $ckanById.ContainsKey($_) })).Count)")
$lines.Add("In CKAN but not truth: $((@($ckanById.Keys | Where-Object { -not $truthById.ContainsKey($_) })).Count)")
$lines.Add("Intersection: $((@($truthById.Keys | Where-Object { $ckanById.ContainsKey($_) })).Count)")
$lines.Add("")
$lines.Add("=== Party disagreements (non-blank CKAN vs truth) ===")
$disagree = 0
foreach ($uid in $truthById.Keys) {
  if (-not $ckanById.ContainsKey($uid)) { continue }
  $tp = $truthById[$uid]['PoliticalParty']
  $cp = $ckanById[$uid]['PoliticalPartyTitle']
  if ([string]::IsNullOrWhiteSpace($cp)) { continue }
  if ($tp -ne $cp) {
    $disagree++
    $lines.Add(("  {0}  {1}" -f $truthById[$uid]['FullName'], $uid))
    $lines.Add(("    CKAN : {0}" -f $cp))
    $lines.Add(("    Truth: {0}" -f $tp))
  }
}
$lines.Add("Total disagreements: $disagree")
$lines.Add("")
$lines.Add("=== Независни (truly independent) MPs ===")
# Find the independent-party label by scanning the distinct set (avoid embedding Cyrillic in this script).
$indepLabels = $truth | ForEach-Object { $_['PoliticalParty'] } | Sort-Object -Unique | Where-Object { $_ -match 'ависн' -or $_ -match 'ndepend' }
foreach ($t in $truth) {
  if ($indepLabels -contains $t['PoliticalParty']) {
    $lines.Add(("  {0}  {1}  Cons={2}  Party={3}" -f $t['UserId'], $t['FullName'], $t['Constituency'], $t['PoliticalParty']))
  }
}
$lines.Add("")
$lines.Add("=== Distinct party names (for slug map) ===")
$parties = $truth | ForEach-Object { $_['PoliticalParty'] } | Sort-Object -Unique
foreach ($p in $parties) { $lines.Add("  $p") }

[IO.File]::WriteAllLines($OutPath, $lines, [Text.UTF8Encoding]::new($false))
Write-Output ("Wrote {0}" -f $OutPath)
