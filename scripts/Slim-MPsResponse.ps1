param(
  [string]$InPath = 'C:\Users\Van4o\sobranie-void\scripts\mps-with-parties.raw.json',
  [string]$OutPath = 'C:\Users\Van4o\sobranie-void\scripts\mps-with-parties.slim.json',
  [string]$SummaryPath = 'C:\Users\Van4o\sobranie-void\scripts\mps-with-parties.summary.txt'
)

Add-Type -AssemblyName System.Web.Extensions
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength = [int]::MaxValue
$serializer.RecursionLimit = 100
$raw = [IO.File]::ReadAllText($InPath, [Text.Encoding]::UTF8)
$parsed = $serializer.DeserializeObject($raw)
if ($parsed.ContainsKey('MembersOfParliament')) { $mps = $parsed['MembersOfParliament'] } else { $mps = $parsed }

$slim = New-Object System.Collections.Generic.List[object]
foreach ($m in $mps) {
  $slim.Add([PSCustomObject]@{
    UserId             = $m['UserId']
    FullName           = $m['FullName']
    FirstName          = $m['FirstName']
    LastName           = $m['LastName']
    PoliticalPartyId   = $m['PoliticalPartyId']
    PoliticalParty     = $m['PoliticalPartyTitle']
    Coalition          = $m['Coalition']
    Constituency       = $m['Constituency']
    DateOfBirth        = $m['DateOfBirth']
    Gender             = $m['Gender']
  })
}

$json = $slim | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($OutPath, $json, [Text.UTF8Encoding]::new($false))

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Total MPs: $($slim.Count)")
$lines.Add("")
$lines.Add("=== Field names on first raw record ===")
if ($mps.Count -gt 0) {
  foreach ($k in $mps[0].Keys) { $lines.Add(("  {0}" -f $k)) }
}
$lines.Add("")
$lines.Add("=== Party distribution (PoliticalPartyTitle) ===")
$groups = $slim | Group-Object PoliticalParty | Sort-Object Count -Descending
foreach ($g in $groups) {
  $label = if ([string]::IsNullOrWhiteSpace($g.Name)) { "(BLANK)" } else { $g.Name }
  $lines.Add(("  {0,3}  {1}" -f $g.Count, $label))
}
$lines.Add("")
$lines.Add("=== MPs with blank party ===")
$blanks = $slim | Where-Object { [string]::IsNullOrWhiteSpace($_.PoliticalParty) }
foreach ($b in $blanks) {
  $lines.Add(("  {0}  {1}" -f $b.UserId, $b.FullName))
}
[IO.File]::WriteAllLines($SummaryPath, $lines, [Text.UTF8Encoding]::new($false))
Write-Output ("Wrote slim={0} ({1} MPs), summary={2}" -f $OutPath, $slim.Count, $SummaryPath)
