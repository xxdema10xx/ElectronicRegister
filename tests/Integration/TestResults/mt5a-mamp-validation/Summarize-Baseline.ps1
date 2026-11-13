$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$rows=Get-Content (Join-Path $root 'baseline-test-identities.json') -Raw|ConvertFrom-Json
$categorized=foreach($r in $rows){
    $category=switch -Regex ($r.Name){
        '\.Mt1IntegrityTests\.' {'MT1';break}
        '\.Mt2(Identity|MicrosoftIdentity)Tests\.' {'MT2';break}
        '\.Mt3aOwnershipTests\.' {'MT3A';break}
        '\.Mt3ProfileTests\.' {'MT3 profiles';break}
        '\.Mt3bSchoolTests\.' {'MT3B';break}
        '\.Mt3c(Http|Preflight)Tests\.' {'MT3C';break}
        '\.Mt3dHttpTests\.Mt3e' {'MT3E';break}
        '\.Mt3dHttpTests\.' {'MT3D';break}
        '\.Mt4aCourseStructureTests\.' {'MT4A';break}
        '\.Mt4bHttpTests\.Mt4c' {'MT4C';break}
        '\.Mt4bHttpTests\.' {'MT4B';break}
        default {if($r.Project-eq 'Unit'){'Unit'}else{'Other integration'}}
    }
    [pscustomobject]@{Category=$category;Name=$r.Name;Outcome=$r.Outcome}
}
$categorized|ConvertTo-Json -Depth 4|Set-Content (Join-Path $root 'baseline-module-identities.json')
$summary=foreach($group in $categorized|Group-Object Category|Sort-Object Name){
    [pscustomobject]@{Module=$group.Name;Total=$group.Count;Passed=@($group.Group|Where-Object Outcome -eq 'Passed').Count;Failed=@($group.Group|Where-Object Outcome -eq 'Failed').Count;Other=@($group.Group|Where-Object Outcome -notin @('Passed','Failed')).Count;FullyPassed=(@($group.Group|Where-Object Outcome -ne 'Passed').Count-eq 0)}
}
$summary|ConvertTo-Json|Set-Content (Join-Path $root 'baseline-module-summary.json')
$rows|Where-Object Name -match 'RealBootstrap|RealProgram_Starts'|ConvertTo-Json|Set-Content (Join-Path $root 'bootstrap-identities.json')
$summary|Format-Table
