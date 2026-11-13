using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Exceptions;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Services;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;

public sealed class InstitutionImportXlsxReaderTests
{
    internal static byte[] Workbook(string[][] rows,bool formula=false,bool number=false,bool external=false,bool multiple=false)
    {
        using var stream=new MemoryStream();
        using(var doc=SpreadsheetDocument.Create(stream,SpreadsheetDocumentType.Workbook,true))
        {
            var book=doc.AddWorkbookPart();book.Workbook=new Workbook();var part=book.AddNewPart<WorksheetPart>();var data=new SheetData();part.Worksheet=new Worksheet(data);
            var sheets=book.Workbook.AppendChild(new Sheets());sheets.Append(new Sheet{Id=book.GetIdOfPart(part),SheetId=1,Name="Studenti"});
            for(var i=0;i<rows.Length;i++)
            {
                var row=new Row{RowIndex=(uint)i+1};
                for(var j=0;j<rows[i].Length;j++)row.Append(new Cell{CellReference=((char)('A'+j)).ToString()+(i+1),DataType=CellValues.InlineString,InlineString=new InlineString(new Text(rows[i][j]))});
                data.Append(row);
            }
            if(formula){var c=data.Elements<Row>().Last().Elements<Cell>().First();c.InlineString=null;c.DataType=CellValues.String;c.CellFormula=new CellFormula("HYPERLINK(\"https://invalid.example/\",\"Mario\")");c.CellValue=new CellValue("Mario");}
            if(number){var c=data.Elements<Row>().Last().Elements<Cell>().First();c.InlineString=null;c.DataType=CellValues.Number;c.CellValue=new CellValue("1234");}
            if(external)part.AddHyperlinkRelationship(new Uri("https://invalid.example/"),true);
            if(multiple){var second=book.AddNewPart<WorksheetPart>();second.Worksheet=new Worksheet(new SheetData());sheets.Append(new Sheet{Id=book.GetIdOfPart(second),SheetId=2,Name="Altro"});}
            part.Worksheet.Save();book.Workbook.Save();
        }
        return stream.ToArray();
    }
    private static Task<ImportTable> Read(byte[] bytes,ImportParsingOptions? options=null,ImportParserLimits? limits=null)=>new InstitutionImportFileReader().ReadAsync(new MemoryStream(bytes),"data.xlsx",options??new(),limits??new());
    [Fact]
    public async Task RealSdkWorkbookPreservesUnicodeAndBlankTrailingCells()
    {var t=await Read(Workbook(new[]{new[]{"Nome","Cognome","Classe"},new[]{"José","García",""},new[]{"Anna Maria","De Luca","A"}}));Assert.Equal("xlsx",t.Delimiter);Assert.Equal(2,t.Rows.Count);Assert.Equal("José",t.Rows[0].Cells[0]);Assert.Equal("",t.Rows[0].Cells[2]);}
    [Theory]
    [InlineData("formula","FormulaNotAllowed")][InlineData("number","UnsupportedCellType")][InlineData("external","ExternalReferenceNotAllowed")]
    public async Task FormulasCachedValuesNumbersAndExternalLinksAreRejected(string kind,string code)
    {var data=Workbook(new[]{new[]{"Nome","Cognome"},new[]{"Mario","Rossi"}},formula:kind=="formula",number:kind=="number",external:kind=="external");Assert.Equal(code,(await Assert.ThrowsAsync<ImportInputException>(()=>Read(data))).Code);}
    [Fact]
    public async Task MultipleSheetsRequireExplicitSelection()
    {var data=Workbook(new[]{new[]{"Nome","Cognome"},new[]{"Mario","Rossi"}},multiple:true);Assert.Equal("WorksheetSelectionRequired",(await Assert.ThrowsAsync<ImportInputException>(()=>Read(data))).Code);Assert.Single((await Read(data,new(WorksheetName:"Studenti"))).Rows);}
    [Theory]
    [InlineData("expansion")][InlineData("nodes")][InlineData("rows")]
    public async Task BoundedXmlAndExpansionLimitsAreEnforced(string kind)
    {var data=Workbook(new[]{new[]{"Nome","Cognome"},new[]{"Mario","Rossi"},new[]{"Anna","Bianchi"}});var limits=kind=="expansion"?new ImportParserLimits(MaxUncompressedBytes:20):kind=="nodes"?new(MaxXmlNodes:2):new ImportParserLimits(MaxDataRows:1);await Assert.ThrowsAsync<ImportInputException>(()=>Read(data,limits:limits));}
    [Theory]
    [InlineData("../outside.xml")][InlineData("xl/%2e%2e/outside.xml")][InlineData("xl/vbaProject.bin")]
    public async Task HostileZipPathsAndBinaryPartsAreRejected(string path)
    {using var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){var entry=zip.CreateEntry(path);using var writer=new StreamWriter(entry.Open());writer.Write("<x/>");}await Assert.ThrowsAsync<ImportInputException>(()=>Read(stream.ToArray()));}
    [Fact]
    public async Task DtdIsProhibitedBeforeSdkParsing()
    {using var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){var entry=zip.CreateEntry("xl/workbook.xml");using var writer=new StreamWriter(entry.Open());writer.Write("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///invalid'>]><x>&a;</x>");}Assert.Equal("MalformedWorkbook",(await Assert.ThrowsAsync<ImportInputException>(()=>Read(stream.ToArray()))).Code);}
}
