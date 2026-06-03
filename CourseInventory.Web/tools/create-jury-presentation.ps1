$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo "Presentations"
$workDir = Join-Path $repo ("obj\pptx-jury-" + [guid]::NewGuid().ToString("N"))
$pptxPath = Join-Path $outDir "Inventory-Studio-Jury-Presentation.pptx"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "docProps") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "ppt\_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "ppt\slides\_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "ppt\slideLayouts\_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "ppt\slideMasters\_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workDir "ppt\theme") | Out-Null

$slides = @(
    @{
        Kicker = "PROJECT OVERVIEW"
        Title = "Inventory Studio centralizes custom inventory management for teams."
        Subtitle = "ASP.NET Core MVC application with PostgreSQL, Identity, SignalR, custom fields, access control, search and exports."
        Bullets = @("Built for exam requirements, not only a demo UI", "Users create inventories, configure fields, manage items and collaborate", "Admins manage accounts, roles and platform access")
        Footer = "Course project presentation"
    },
    @{
        Kicker = "PROBLEM"
        Title = "Generic spreadsheets cannot handle shared, secure and customizable inventories."
        Subtitle = "The project solves the gap between simple item lists and real collaborative inventory workflows."
        Bullets = @("Each inventory can define its own structure and ID format", "Access can be public, private or limited to selected users", "Every major workflow remains table-based and searchable")
        Footer = "Why this application exists"
    },
    @{
        Kicker = "SOLUTION"
        Title = "The application is organized around inventories, items and configurable rules."
        Subtitle = "The user starts from an inventory, then configures tabs for items, discussion, settings, custom IDs, access, fields and statistics."
        Bullets = @("Inventory settings: title, description, category, image URL, tags, visibility", "Items inherit custom fields and generated Custom IDs", "Tabs separate operational tasks and reduce UI complexity")
        Footer = "Functional model"
    },
    @{
        Kicker = "ARCHITECTURE"
        Title = "The codebase follows a clean MVC + Services + EF Core structure."
        Subtitle = "Controllers coordinate requests, services contain business logic, EF Core maps the PostgreSQL database, and Razor views render the interface."
        Bullets = @("Controllers: Inventories, Items, Account, Admin, Search, Access, Fields, Custom ID", "Services: access, inventory, item, fields, stats, tags, discussion, search", "Data layer: ApplicationDbContext, migrations and domain models")
        Footer = "Technical architecture"
    },
    @{
        Kicker = "DATABASE"
        Title = "PostgreSQL stores relational data with concurrency, indexes and full-text search."
        Subtitle = "The database model supports users, inventories, items, fields, custom ID elements, access grants, tags, comments and likes."
        Bullets = @("EF Core + Npgsql with migrations and seeded roles", "RowVersion optimistic locking on inventories and items", "Unique constraints for Custom ID per inventory and one like per user/item")
        Footer = "Data integrity"
    },
    @{
        Kicker = "REQUIREMENTS"
        Title = "Mandatory examiner requirements are mapped directly to implemented features."
        Subtitle = "The project includes theme/language support, responsive Bootstrap UI, tables, search, custom fields, real-time discussion, likes and admin controls."
        Bullets = @("Light/dark theme and English/French UI text", "Full-text search and table-based main views", "Custom fields, tags autocomplete, access autocomplete and statistics tab")
        Footer = "Checklist coverage"
    },
    @{
        Kicker = "SECURITY"
        Title = "Access control is enforced server-side, not only hidden in the interface."
        Subtitle = "Every sensitive operation checks the current user role and inventory permissions before reading or writing data."
        Bullets = @("Anonymous users can read public inventories and search only", "Owners and admins can manage inventories; explicit users can write", "Blocked users cannot log in; admins manage roles and accounts")
        Footer = "Security model"
    },
    @{
        Kicker = "REAL TIME"
        Title = "SignalR powers real-time discussion and online presence per inventory."
        Subtitle = "The discussion tab lets authenticated users collaborate directly inside an inventory."
        Bullets = @("Users join inventory-specific SignalR groups", "Messages are stored as markdown and rendered as safe HTML", "Presence updates show who is online in the discussion")
        Footer = "Collaboration"
    },
    @{
        Kicker = "QUALITY"
        Title = "Recent refactoring makes the project easier to explain, maintain and defend."
        Subtitle = "Large Razor files were split into partial views and helper classes while preserving behavior."
        Bullets = @("Details page is now an orchestrator with focused partials", "Item edit dynamic fields moved to a dedicated partial", "Helpers isolate formatting and validation display logic")
        Footer = "Maintainability"
    },
    @{
        Kicker = "DEMO PLAN"
        Title = "The jury can verify the project through five short workflows."
        Subtitle = "A focused demo is enough to prove the main technical and functional requirements."
        Bullets = @("1. Register/login and switch language/theme", "2. Create an inventory, add fields and configure Custom ID drag-and-drop", "3. Add/edit items, like them and inspect stats", "4. Share access with autocomplete and test read-only public access", "5. Open admin panel and show role/block management")
        Footer = "Presentation flow"
    },
    @{
        Kicker = "CONCLUSION"
        Title = "Inventory Studio is a complete, database-backed collaborative inventory platform."
        Subtitle = "The strongest points are its custom configuration, secure access model, searchable tables, real-time discussion and maintainable ASP.NET Core architecture."
        Bullets = @("Meets the core mandatory requirements", "Demonstrates backend, database, frontend and real-time skills", "Ready for jury demonstration with clear technical explanations")
        Footer = "Final message"
    }
)

function XmlEscape([string]$value) {
    if ($null -eq $value) { return "" }
    return [System.Security.SecurityElement]::Escape($value)
}

function PtToEmu([double]$pt) {
    return [int64]($pt * 12700)
}

function AddTextShape([int]$id, [string]$name, [int]$x, [int]$y, [int]$cx, [int]$cy, [string[]]$lines, [int]$fontSize, [string]$color, [bool]$bold = $false) {
    $b = if ($bold) { ' b="1"' } else { "" }
    $paragraphs = foreach ($line in $lines) {
        '<a:p><a:r><a:rPr lang="fr-FR" sz="' + ($fontSize * 100) + '"' + $b + '><a:solidFill><a:srgbClr val="' + $color + '"/></a:solidFill><a:latin typeface="Aptos"/></a:rPr><a:t>' + (XmlEscape $line) + '</a:t></a:r></a:p>'
    }
    return @"
<p:sp>
  <p:nvSpPr><p:cNvPr id="$id" name="$name"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="$(PtToEmu $x)" y="$(PtToEmu $y)"/><a:ext cx="$(PtToEmu $cx)" cy="$(PtToEmu $cy)"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></p:spPr>
  <p:txBody><a:bodyPr wrap="square"/><a:lstStyle/>$($paragraphs -join "")
  </p:txBody>
</p:sp>
"@
}

function AddRect([int]$id, [string]$name, [int]$x, [int]$y, [int]$cx, [int]$cy, [string]$fill, [string]$line = "2A3A52") {
    return @"
<p:sp>
  <p:nvSpPr><p:cNvPr id="$id" name="$name"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
  <p:spPr><a:xfrm><a:off x="$(PtToEmu $x)" y="$(PtToEmu $y)"/><a:ext cx="$(PtToEmu $cx)" cy="$(PtToEmu $cy)"/></a:xfrm><a:prstGeom prst="roundRect"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="$fill"/></a:solidFill><a:ln w="12700"><a:solidFill><a:srgbClr val="$line"/></a:solidFill></a:ln></p:spPr>
</p:sp>
"@
}

function MakeSlideXml($slide, [int]$index) {
    $shapes = @()
    $shapes += AddRect 10 "Accent rail" 36 44 10 430 "2F6BFF" "2F6BFF"
    $shapes += AddTextShape 11 "Kicker" 60 42 380 24 @($slide.Kicker) 11 "69A7FF" $true
    $shapes += AddTextShape 12 "Title" 60 76 790 110 @($slide.Title) 28 "F5F7FB" $true
    $shapes += AddTextShape 13 "Subtitle" 60 188 760 52 @($slide.Subtitle) 13 "AFC3E6"

    $y = 276
    $id = 20
    foreach ($bullet in $slide.Bullets) {
        $shapes += AddRect $id "Bullet marker $id" 76 ($y + 6) 7 7 "69A7FF" "69A7FF"
        $shapes += AddTextShape ($id + 1) "Bullet $id" 96 $y 720 30 @($bullet) 15 "E6EDF8"
        $y += 48
        $id += 2
    }

    $shapes += AddRect 60 "Right panel" 910 76 250 378 "172131" "27364D"
    $shapes += AddTextShape 61 "Panel title" 936 104 205 32 @("Key proof") 14 "69A7FF" $true
    $shapes += AddTextShape 62 "Panel metric" 936 150 190 54 @("ASP.NET", "Core MVC") 24 "F5F7FB" $true
    $shapes += AddTextShape 63 "Panel text" 936 240 190 120 @("PostgreSQL", "Identity", "SignalR", "Bootstrap", "EF Core") 15 "AFC3E6"
    $shapes += AddTextShape 70 "Footer" 60 668 740 18 @($slide.Footer) 9 "6F84A6"
    $shapes += AddTextShape 71 "Page" 1114 668 60 18 @("$index / $($slides.Count)") 9 "6F84A6"

    return @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld>
    <p:bg><p:bgPr><a:solidFill><a:srgbClr val="0D1420"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>
    <p:spTree>
      <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
      <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
      $($shapes -join "`n")
    </p:spTree>
  </p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sld>
"@
}

Set-Content -LiteralPath (Join-Path $workDir "[Content_Types].xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
  <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>
  <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>
  <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>
  <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>
  $(for ($i=1; $i -le $slides.Count; $i++) { '<Override PartName="/ppt/slides/slide' + $i + '.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slide+xml"/>' })
</Types>
"@

Set-Content -Path (Join-Path $workDir "_rels\.rels") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"@

Set-Content -Path (Join-Path $workDir "docProps\core.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>Inventory Studio Jury Presentation</dc:title>
  <dc:creator>CourseInventory.Web</dc:creator>
  <cp:lastModifiedBy>CourseInventory.Web</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">2026-05-26T00:00:00Z</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">2026-05-26T00:00:00Z</dcterms:modified>
</cp:coreProperties>
"@

Set-Content -Path (Join-Path $workDir "docProps\app.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft PowerPoint</Application>
  <PresentationFormat>On-screen Show (16:9)</PresentationFormat>
  <Slides>$($slides.Count)</Slides>
</Properties>
"@

$slideIds = for ($i=1; $i -le $slides.Count; $i++) { '<p:sldId id="' + (255 + $i) + '" r:id="rId' + $i + '"/>' }
Set-Content -Path (Join-Path $workDir "ppt\presentation.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId$($slides.Count + 1)"/></p:sldMasterIdLst>
  <p:sldIdLst>$($slideIds -join "")</p:sldIdLst>
  <p:sldSz cx="12192000" cy="6858000" type="wide"/>
  <p:notesSz cx="6858000" cy="9144000"/>
  <p:defaultTextStyle/>
</p:presentation>
"@

$presentationRels = for ($i=1; $i -le $slides.Count; $i++) { '<Relationship Id="rId' + $i + '" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide' + $i + '.xml"/>' }
$presentationRels += '<Relationship Id="rId' + ($slides.Count + 1) + '" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml"/>'
Set-Content -Path (Join-Path $workDir "ppt\_rels\presentation.xml.rels") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  $($presentationRels -join "`n")
</Relationships>
"@

Set-Content -Path (Join-Path $workDir "ppt\slideMasters\slideMaster1.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
  <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>
  <p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>
</p:sldMaster>
"@
Set-Content -Path (Join-Path $workDir "ppt\slideMasters\_rels\slideMaster1.xml.rels") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml"/>
</Relationships>
"@
Set-Content -Path (Join-Path $workDir "ppt\slideLayouts\slideLayout1.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1">
  <p:cSld name="Blank"><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
</p:sldLayout>
"@
Set-Content -Path (Join-Path $workDir "ppt\slideLayouts\_rels\slideLayout1.xml.rels") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml"/>
</Relationships>
"@
Set-Content -Path (Join-Path $workDir "ppt\theme\theme1.xml") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Inventory Studio">
  <a:themeElements>
    <a:clrScheme name="Inventory Studio"><a:dk1><a:srgbClr val="0D1420"/></a:dk1><a:lt1><a:srgbClr val="F5F7FB"/></a:lt1><a:dk2><a:srgbClr val="172131"/></a:dk2><a:lt2><a:srgbClr val="AFC3E6"/></a:lt2><a:accent1><a:srgbClr val="2F6BFF"/></a:accent1><a:accent2><a:srgbClr val="69A7FF"/></a:accent2><a:accent3><a:srgbClr val="22C55E"/></a:accent3><a:accent4><a:srgbClr val="F59E0B"/></a:accent4><a:accent5><a:srgbClr val="EF4444"/></a:accent5><a:accent6><a:srgbClr val="8B5CF6"/></a:accent6><a:hlink><a:srgbClr val="69A7FF"/></a:hlink><a:folHlink><a:srgbClr val="A78BFA"/></a:folHlink></a:clrScheme>
    <a:fontScheme name="Aptos"><a:majorFont><a:latin typeface="Aptos Display"/></a:majorFont><a:minorFont><a:latin typeface="Aptos"/></a:minorFont></a:fontScheme>
    <a:fmtScheme name="Inventory Studio"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="12700"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>
  </a:themeElements>
</a:theme>
"@

for ($i=1; $i -le $slides.Count; $i++) {
    Set-Content -Path (Join-Path $workDir "ppt\slides\slide$i.xml") -Value (MakeSlideXml $slides[$i-1] $i)
    Set-Content -Path (Join-Path $workDir "ppt\slides\_rels\slide$i.xml.rels") -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
</Relationships>
"@
}

if (Test-Path $pptxPath) {
    Remove-Item -LiteralPath $pptxPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($pptxPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $workDir -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($workDir.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host $pptxPath
