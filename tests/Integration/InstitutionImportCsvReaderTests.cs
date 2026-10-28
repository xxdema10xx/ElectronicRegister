using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Exceptions;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Services;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

// Pure infrastructure-reader tests: no database or HTTP execution is claimed here.
public sealed class InstitutionImportCsvReaderTests
{
    private readonly InstitutionImportFileReader reader=new();
    private Task<ImportTable> Read(string text,string? delimiter=null,ImportParserLimits? limits=null)=>reader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)),"students.csv",new(delimiter),limits??new());
    [Theory]
    [InlineData(";")][InlineData(",")]
    public async Task Utf8DelimiterAndBomAreHandled(string delimiter)
    {var text="\uFEFFNome"+delimiter+"Cognome\r\nJosé"+delimiter+"García\r\n";var t=await Read(text);Assert.Equal(delimiter,t.Delimiter);Assert.Equal(new[]{"Nome","Cognome"},t.Headers);Assert.Equal("José",Assert.Single(t.Rows).Cells[0]);Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))),t.Sha256);}
    [Fact]
    public async Task Utf8WithoutBomAndQuotedCommaPreserveUnicodeAndNames()
    {var t=await Read("First Name,Last Name\n\"José, Mario\",García\n",",");Assert.Equal("José, Mario",Assert.Single(t.Rows).Cells[0]);Assert.Equal("García",t.Rows[0].Cells[1]);}
    [Fact]
    public async Task DisguisedBinaryAndInvalidSeparatorsAreRejected()
    {await Assert.ThrowsAsync<ImportInputException>(()=>reader.ReadAsync(new MemoryStream(new byte[]{0x50,0x4b,0,0}),"file.csv",new(";"),new()));Assert.Equal("InvalidDelimiter",(await Assert.ThrowsAsync<ImportInputException>(()=>Read("A;B","|"))).Code);}
    [Fact]
    public async Task QuotingEscapesSeparatorsMultilineAndEmptyRowsAreParsed()
    {var t=await Read("Nome;Cognome\n\n\"Anna; Maria\";\"D'Angelo\"\n\"Jean\"\"Pierre\";\"De\nLuca\"\n\n", ";");Assert.Equal(2,t.Rows.Count);Assert.Equal("Anna; Maria",t.Rows[0].Cells[0]);Assert.Equal("Jean\"Pierre",t.Rows[1].Cells[0]);Assert.Equal("De\nLuca",t.Rows[1].Cells[1]);Assert.Equal(3,t.Rows[0].RowNumber);Assert.Equal(4,t.Rows[1].RowNumber);}
    [Fact]
    public async Task LeadingTrailingSpacesAndTrailingEmptyCellRemainAvailableForValidation()
    {var t=await Read(" Nome ; Cognome ; Classe \n Mario ; Rossi ;\n", ";");Assert.Equal(new[]{"Nome","Cognome","Classe"},t.Headers);Assert.Equal(" Mario ",t.Rows[0].Cells[0]);Assert.Equal("",t.Rows[0].Cells[2]);}
    [Theory]
    [InlineData("Nome;Cognome\n\"Mario;Rossi", "MalformedCsv")]
    [InlineData("Nome;Cognome\nMa\"rio;Rossi", "MalformedCsv")]
    [InlineData("Nome;Cognome\n\"Mario\"x;Rossi", "MalformedCsv")]
    [InlineData("Nome;Cognome\nMario;Rossi;Extra", "ColumnCountMismatch")]
    [InlineData("Nome;;Cognome\nMario;;Rossi", "InvalidHeader")]
    [InlineData("\n\n", "EmptyFile")]
    public async Task MalformedStructureIsRejected(string text,string code)=>Assert.Equal(code,(await Assert.ThrowsAsync<ImportInputException>(()=>Read(text,";"))).Code);
    [Theory]
    [InlineData("students.xlsx")][InlineData("students.xlsm")][InlineData("students.xlsb")][InlineData("students.xls")][InlineData("students.exe")]
    public async Task UnsupportedFilesFailClosedBeforeParsing(string name)=>Assert.Equal(name.EndsWith(".xlsx")?"MalformedWorkbook":"UnsupportedFormat",(await Assert.ThrowsAsync<ImportInputException>(()=>reader.ReadAsync(new MemoryStream(new byte[]{0x50,0x4b}),name,new(),new()))).Code);
    [Fact]
    public async Task InvalidUtf8IsNotSilentlyConverted()
    {Assert.Equal("InvalidEncoding",(await Assert.ThrowsAsync<ImportInputException>(()=>reader.ReadAsync(new MemoryStream(new byte[]{0xff,0xfe,0x00,0x01}),"file.csv",new(),new()))).Code);Assert.Equal("UnsupportedEncoding",(await Assert.ThrowsAsync<ImportInputException>(()=>reader.ReadAsync(new MemoryStream(),"file.csv",new(Encoding:"windows-1252"),new()))).Code);}
    [Fact]
    public async Task SizeIsMeasuredFromActualBytesNotReportedLength()
    {var stream=new MemoryStream(Encoding.UTF8.GetBytes("Nome;Cognome\nMario;Rossi"));Assert.Equal("FileTooLarge",(await Assert.ThrowsAsync<ImportInputException>(()=>reader.ReadAsync(stream,"file.csv",new(";"),new(MaxFileBytes:10)))).Code);Assert.Equal(11,stream.Position);}
    [Theory]
    [InlineData("rows")][InlineData("columns")][InlineData("cell")][InlineData("record")]
    public async Task ResourceLimitsAreEnforced(string kind)
    {var limits=kind=="rows"?new ImportParserLimits(MaxDataRows:1):kind=="columns"?new(MaxColumns:2):kind=="cell"?new(MaxCellCharacters:3):new ImportParserLimits(MaxCellCharacters:20,MaxRecordCharacters:20);var text=kind=="rows"?"Nome;Cognome\na;b\nc;d":kind=="columns"?"A;B;C\na;b;c":kind=="cell"?"A;B\nlong;b":"A;B\nabcdefghijklmnop;qwerty";await Assert.ThrowsAsync<ImportInputException>(()=>Read(text,";",limits));}
    [Fact]
    public async Task AmbiguousDelimiterRequiresExplicitChoice()
    {Assert.Equal("AmbiguousDelimiter",(await Assert.ThrowsAsync<ImportInputException>(()=>Read("A;B,C\na;b,c"))).Code);Assert.Equal(2,(await Read("A;B,C\na;b,c",";")).Headers.Count);}
    [Fact]
    public async Task CancellationAndBadLimitsAreHonored()
    {await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reader.ReadAsync(new MemoryStream(),"file.csv",new(),new(),new CancellationToken(true)));await Assert.ThrowsAsync<ArgumentException>(()=>Read("A;B",";",new(MaxFileBytes:int.MaxValue)));}
}
