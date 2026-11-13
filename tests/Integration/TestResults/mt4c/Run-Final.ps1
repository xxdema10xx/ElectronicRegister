$ErrorActionPreference='Stop'
$root='tests/Integration/TestResults/mt4c'
$ns=@{t='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore *> "$root/build-final.log"
if($LASTEXITCODE){throw 'final build failed'}
python "$root/Verify-Sources.py" freeze
if($LASTEXITCODE){throw 'source freeze failed'}
$env:TEST_MYSQL_CONNECTION='Server=127.0.0.1;Port=3306;User=root;Password=;Database=electronic_register_test'
$env:ER_TEST_STRICT_SESSION='1';$env:Logging__EventLog__LogLevel__Default='None';$env:MYSQLDUMP_EXE='C:/xampp/mysql/bin/mysqldump.exe';$env:MYSQLDUMP_FLAVOR='MariaDB'
foreach($runNumber in 1,2){
 python "$root/Verify-Sources.py" verify
 if($LASTEXITCODE){throw 'source mismatch'}
 dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --logger trx --results-directory "$root/final-$runNumber" *> "$root/final-$runNumber.log"
 if($LASTEXITCODE){throw "final-$runNumber failed"}
 $passed=0;$files=Get-ChildItem "$root/final-$runNumber" -Filter *.trx
 if($files.Count -ne 2){throw 'missing project TRX'}
 foreach($file in $files){
  [xml]$trx=Get-Content $file.FullName;$c=(Select-Xml -Xml $trx -Namespace $ns -XPath '//t:Counters').Node
  if($c.failed -ne '0' -or $c.notExecuted -ne '0' -or $c.total -ne $c.passed -or $c.executed -ne $c.total){throw 'non-green TRX'}
  $passed += [int]$c.passed
 }
 if($passed -ne 675){throw "unexpected total $passed"}
}
python "$root/Verify-Sources.py" verify
if($LASTEXITCODE){throw 'final source mismatch'}
python "$root/Verify-Evidence.py"
if($LASTEXITCODE){throw 'evidence mismatch'}
