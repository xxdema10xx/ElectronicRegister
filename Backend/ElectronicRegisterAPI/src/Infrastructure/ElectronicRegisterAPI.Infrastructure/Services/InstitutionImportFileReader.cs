using System.Security.Cryptography;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Services;

/// <summary>Bounded UTF-8 CSV reader and supported Open XML SDK adapter with ZIP/XML preflight.</summary>
public sealed partial class InstitutionImportFileReader : IInstitutionImportFileReader
{
    public async Task<ImportTable> ReadAsync(Stream input,string fileName,ImportParsingOptions options,ImportParserLimits limits,CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        var extension=Path.GetExtension(fileName)?.ToLowerInvariant();
        if(extension is not(".csv" or ".xlsx"))
            throw new ImportInputException("UnsupportedFormat","Formato non supportato; utilizzare CSV UTF-8 o XLSX.");
        if(options.Encoding!="utf-8")throw new ImportInputException("UnsupportedEncoding","Selezionare UTF-8; nessuna conversione automatica di codifiche legacy.");
        if(options.Delimiter is not(null or ";" or ","))throw new ImportInputException("InvalidDelimiter","Separatore ammesso: punto e virgola oppure virgola.");
        using var bytes=new MemoryStream();var buffer=new byte[Math.Min(8192,limits.MaxFileBytes+1)];
        while(true)
        {
            ct.ThrowIfCancellationRequested();
            var read=await input.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,limits.MaxFileBytes-bytes.Length+1)),ct);
            if(read==0)break;
            if(bytes.Length+read>limits.MaxFileBytes)throw new ImportInputException("FileTooLarge","Dimensione massima del file superata.");
            await bytes.WriteAsync(buffer.AsMemory(0,read),ct);
        }
        var raw=bytes.ToArray();var hash=Convert.ToHexString(SHA256.HashData(raw));
        if(extension==".xlsx")return ReadWorkbook(raw,hash,options,limits,ct);
        if(options.WorksheetName is not null)throw new ImportInputException("InvalidParsingOptions","La selezione del foglio richiede XLSX.");
        string text;
        try{text=new UTF8Encoding(false,true).GetString(raw);}
        catch(DecoderFallbackException){throw new ImportInputException("InvalidEncoding","Il file non contiene UTF-8 valido.");}
        if(text.StartsWith('\uFEFF'))text=text[1..];
        if(text.IndexOf('\0')>=0)throw new ImportInputException("InvalidEncoding","Contenuto binario o codifica non supportata.");
        if(options.Delimiter is {} selected)return Parse(text,selected[0],hash,limits,ct);
        var candidates=new List<ImportTable>();var failures=new List<ImportInputException>();
        foreach(var delimiter in new[]{';',','})
        {
            try{var candidate=Parse(text,delimiter,hash,limits,ct);if(candidate.Headers.Count>1)candidates.Add(candidate);}
            catch(ImportInputException e){failures.Add(e);}
        }
        if(candidates.Count==1)return candidates[0];
        if(candidates.Count==2)throw new ImportInputException("AmbiguousDelimiter","Specificare esplicitamente il separatore CSV.");
        if(failures.Count==2&&failures[0].Code==failures[1].Code)throw failures[0];
        throw new ImportInputException("DelimiterNotDetected","Separatore non determinabile; specificarlo esplicitamente.");
    }

    private static ImportTable Parse(string text,char delimiter,string hash,ImportParserLimits limits,CancellationToken ct)
    {
        var records=new List<ImportRawRow>();var cells=new List<string>();var cell=new StringBuilder();
        var quoted=false;var closedQuote=false;var line=1;var startLine=1;var recordCharacters=0;
        void Fail(string code,string message)=>throw new ImportInputException(code,message);
        void AddCell()
        {
            if(cells.Count>=limits.MaxColumns)Fail("TooManyColumns","Numero massimo colonne superato.");
            cells.Add(cell.ToString());cell.Clear();closedQuote=false;
        }
        void AddRecord()
        {
            AddCell();
            if(cells.Any(c=>!string.IsNullOrWhiteSpace(c)))
            {
                if(records.Count>=limits.MaxDataRows+1)Fail("TooManyRows","Numero massimo righe dati superato.");
                records.Add(new(startLine,cells.ToArray()));
            }
            cells.Clear();recordCharacters=0;startLine=line;
        }
        for(var i=0;i<text.Length;i++)
        {
            if((i&4095)==0)ct.ThrowIfCancellationRequested();
            var c=text[i];
            if(++recordCharacters>limits.MaxRecordCharacters)Fail("RecordTooLong","Lunghezza massima record CSV superata.");
            if(quoted)
            {
                if(c=='"')
                {
                    if(i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;recordCharacters++;}
                    else{quoted=false;closedQuote=true;}
                }
                else {cell.Append(c);if(c=='\n'||c=='\r'&&(i+1==text.Length||text[i+1]!='\n'))line++;}
            }
            else if(c==delimiter)AddCell();
            else if(c is '\n' or '\r')
            {
                if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;
                line++;AddRecord();startLine=line;
            }
            else if(c=='"')
            {
                if(closedQuote||cell.ToString().Any(ch=>ch is not(' ' or '\t')))Fail("MalformedCsv","Virgolette CSV non valide.");
                cell.Clear();quoted=true;
            }
            else if(closedQuote)
            {if(c is not(' ' or '\t'))Fail("MalformedCsv","Carattere non ammesso dopo la chiusura delle virgolette.");}
            else cell.Append(c);
            if(cell.Length>limits.MaxCellCharacters)Fail("CellTooLong","Lunghezza massima cella superata.");
            if(recordCharacters>limits.MaxRecordCharacters)Fail("RecordTooLong","Lunghezza massima record CSV superata.");
        }
        if(quoted)Fail("MalformedCsv","Campo CSV non terminato.");
        if(cell.Length>0||cells.Count>0||closedQuote)AddRecord();
        if(records.Count==0)Fail("EmptyFile","Il file non contiene intestazioni.");
        var headers=records[0].Cells.Select(c=>c.Trim()).ToArray();
        if(headers.Any(h=>h.Length is <1 or >128||h.Any(char.IsControl)))Fail("InvalidHeader","Intestazione vuota, troppo lunga o non valida.");
        foreach(var row in records.Skip(1))if(row.Cells.Count!=headers.Length)Fail("ColumnCountMismatch","Numero di celle diverso dalle intestazioni; verificare il separatore.");
        return new(hash,delimiter.ToString(),headers,records.Skip(1).ToArray());
    }
}
