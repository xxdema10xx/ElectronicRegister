using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Exceptions;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Infrastructure.Services;

public sealed partial class InstitutionImportFileReader
{
    private static ImportTable ReadWorkbook(
        byte[] bytes,
        string hash,
        ImportParsingOptions options,
        ImportParserLimits limits,
        CancellationToken ct,
        Func<Cell, WorkbookPart, IReadOnlyList<string>, int, string>? calendarNumericCell = null
    )
    {
        if (options.Delimiter is not null)
            throw new ImportInputException("InvalidParsingOptions", "Il separatore è un'opzione CSV.");
        try
        {
            PreflightZip(bytes, limits, ct);
            using var source = new MemoryStream(bytes, false);
            using var document = SpreadsheetDocument.Open(source, false, new OpenSettings { AutoSave = false, MaxCharactersInPart = limits.MaxUncompressedBytes });
            if (document.DocumentType != SpreadsheetDocumentType.Workbook || document.WorkbookPart is not { } workbook || workbook.VbaProjectPart is not null)
                throw BadWorkbook("UnsupportedWorkbook", "Il documento non è un workbook XLSX senza macro.");
            var sheets = workbook.Workbook?.Sheets?.Elements<Sheet>().ToArray() ?? [];
            var selected = options.WorksheetName is null ? sheets.Length == 1 ? sheets[0] : null : sheets.SingleOrDefault(s => s.Name?.Value == options.WorksheetName);
            if (selected?.Id?.Value is not { } relation)
                throw BadWorkbook("WorksheetSelectionRequired", "Selezionare un foglio esistente senza ambiguità.");
            if (workbook.GetPartById(relation)is not WorksheetPart part)
                throw BadWorkbook("UnsupportedWorkbook", "Il foglio selezionato non è una worksheet.");
            var strings = new List<string>();
            if (workbook.SharedStringTablePart is { } shared)
            {
                using var sr = OpenXmlReader.Create(shared);
                while (sr.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (sr.IsStartElement && sr.ElementType == typeof(SharedStringItem))
                    {
                        if (strings.Count >= (long)limits.MaxDataRows * limits.MaxColumns + limits.MaxColumns)
                            throw BadWorkbook("TooManySharedStrings", "Tabella stringhe XLSX oltre il limite.");
                        var item = (SharedStringItem)sr.LoadCurrentElement()!;
                        var value = TextValue(item);
                        if (value.Length > limits.MaxCellCharacters)
                            throw BadWorkbook("CellTooLong", "Lunghezza massima cella superata.");
                        strings.Add(value);
                    }
                }
            }

            var records = new List<ImportRawRow>();
            var cells = new List<string>();
            var rowNumber = 0;
            var previousRow = 0;
            var lastColumn = 0;
            void FinishRow()
            {
                if (rowNumber == 0)
                    return;
                if (cells.Any(s => !string.IsNullOrWhiteSpace(s)))
                {
                    if (records.Count >= limits.MaxDataRows + 1)
                        throw BadWorkbook("TooManyRows", "Numero massimo righe dati superato.");
                    if (cells.Sum(c => c.Length) > limits.MaxRecordCharacters)
                        throw BadWorkbook("RecordTooLong", "Lunghezza massima riga superata.");
                    records.Add(new(rowNumber, cells.ToArray()));
                }

                cells.Clear();
                lastColumn = 0;
                rowNumber = 0;
            }

            using var reader = OpenXmlReader.Create(part);
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                if (reader.ElementType == typeof(Row) && reader.IsStartElement)
                {
                    FinishRow();
                    var r = reader.Attributes.FirstOrDefault(a => a.LocalName == "r").Value;
                    rowNumber = r is null ? previousRow + 1 : int.TryParse(r, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
                    if (rowNumber <= previousRow || rowNumber > 1048576)
                        throw BadWorkbook("MalformedWorkbook", "Numerazione righe XLSX non valida.");
                    previousRow = rowNumber;
                }
                else if (reader.ElementType == typeof(Row) && reader.IsEndElement)
                    FinishRow();
                else if (reader.ElementType == typeof(Cell) && reader.IsStartElement)
                {
                    if (rowNumber == 0)
                        throw BadWorkbook("MalformedWorkbook", "Cella fuori da una riga.");
                    var cell = (Cell)reader.LoadCurrentElement()!;
                    if (cell.CellFormula is not null)
                        throw BadWorkbook("FormulaNotAllowed", "Le celle formula non sono ammesse, inclusi i valori cached.");
                    var column = lastColumn + 1;
                    if (cell.CellReference?.Value is { } reference)
                    {
                        column = 0;
                        var index = 0;
                        while (index < reference.Length && reference[index] is >= 'A' and <= 'Z')
                        {
                            column = checked(column * 26 + reference[index++] - 'A' + 1);
                            if (column > limits.MaxColumns)
                                throw BadWorkbook("TooManyColumns", "Numero massimo colonne superato.");
                        }

                        if (index == 0 || !int.TryParse(reference.AsSpan(index), NumberStyles.None, CultureInfo.InvariantCulture, out var cellRow) || cellRow != rowNumber)
                            throw BadWorkbook("MalformedWorkbook", "Riferimento cella non valido.");
                    }

                    if (column <= lastColumn || column > limits.MaxColumns)
                        throw BadWorkbook("TooManyColumns", "Colonne duplicate, fuori ordine o oltre limite.");
                    while (cells.Count < column - 1)
                        cells.Add("");
                    var value = cell.DataType?.Value == CellValues.InlineString ? TextValue(cell) : cell.CellValue?.Text ?? "";
                    if (cell.DataType?.Value == CellValues.SharedString)
                    {
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 0 || id >= strings.Count)
                            throw BadWorkbook("MalformedWorkbook", "Indice shared string non valido.");
                        value = strings[id];
                    }
                    else if (value.Length > 0 && cell.DataType?.Value != CellValues.InlineString && cell.DataType?.Value != CellValues.String)
                    {
                        if (calendarNumericCell is not null && records.Count > 0 && (cell.DataType is null || cell.DataType.Value == CellValues.Number))
                            value = calendarNumericCell(cell, workbook, records[0].Cells, column - 1);
                        else
                            throw BadWorkbook("UnsupportedCellType", "Usare celle testo per nomi e riferimenti; numeri, date, booleani ed errori non vengono convertiti.");
                    }

                    if (value.Length > limits.MaxCellCharacters)
                        throw BadWorkbook("CellTooLong", "Lunghezza massima cella superata.");
                    cells.Add(value);
                    lastColumn = column;
                }
            }

            FinishRow();
            if (records.Count == 0)
                throw BadWorkbook("EmptyFile", "Il foglio non contiene intestazioni.");
            var headers = records[0].Cells.Select(c => c.Trim()).ToArray();
            if (headers.Any(h => string.IsNullOrEmpty(h) || h.Length > 128 || h.Any(char.IsControl)))
                throw BadWorkbook("InvalidHeader", "Intestazioni XLSX non valide.");
            var rows = records.Skip(1).Select(r => r.Cells.Count > headers.Length ? throw BadWorkbook("ColumnCountMismatch", "Celle oltre le intestazioni.") : r with { Cells = r.Cells.Concat(Enumerable.Repeat("", headers.Length - r.Cells.Count)).ToArray() }).ToArray();
            return new(hash, "xlsx", headers, rows);
        }
        catch (ImportInputException)
        {
            throw;
        }
        catch (CalendarValidationException e)
        {
            throw BadWorkbook(e.Code, "Valore calendario Excel non valido.");
        }
        catch (Exception e)when (e is InvalidDataException or IOException or OpenXmlPackageException or XmlException or FormatException or ArgumentException or OverflowException or InvalidOperationException or NotSupportedException or KeyNotFoundException)
        {
            throw BadWorkbook("MalformedWorkbook", "Struttura XLSX non valida o non supportata.");
        }
    }

    private static string TextValue(OpenXmlElement element) => string.Concat(element.Descendants<Text>().Where(t => !t.Ancestors<PhoneticRun>().Any()).Select(t => t.Text));
    private static ImportInputException BadWorkbook(string code, string message) => new(code, message);
    private static void PreflightZip(byte[] bytes, ImportParserLimits limits, CancellationToken ct)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
        if (zip.Entries.Count > limits.MaxZipEntries)
            throw BadWorkbook("ZipLimitExceeded", "Numero parti XLSX oltre il limite.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        var nodes = 0;
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (name.Length > 256 || name.Contains('\\') || name.Contains(':') || name.Contains('%') || name.StartsWith('/') || name.Split('/').Any(s => s is "." or "..") || !names.Add(name))
                throw BadWorkbook("HostileWorkbook", "Percorsi o parti XLSX non validi.");
            if (name.EndsWith('/'))
                continue;
            if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                throw BadWorkbook("UnsupportedWorkbook", "Parti binarie, macro e oggetti incorporati non sono ammessi.");
            if (entry.Length > limits.MaxUncompressedBytes || entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length / Math.Max(1, entry.CompressedLength) > 1000))
                throw BadWorkbook("ZipLimitExceeded", "Espansione XLSX oltre il limite.");
            using var inflated = new MemoryStream();
            using (var source = entry.Open())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    expanded += count;
                    if (expanded > limits.MaxUncompressedBytes)
                        throw BadWorkbook("ZipLimitExceeded", "Espansione effettiva XLSX oltre il limite.");
                    inflated.Write(buffer, 0, count);
                }
            }

            inflated.Position = 0;
            using var xml = XmlReader.Create(inflated, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.MaxUncompressedBytes });
            var containerDepth = -1;
            var containerNodes = 0;
            var containerText = 0;
            while (xml.Read())
            {
                if (++nodes > limits.MaxXmlNodes || xml.Depth > 32)
                    throw BadWorkbook("XmlLimitExceeded", "Complessità XML oltre il limite.");
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName is "c" or "si")
                    {
                        if (containerDepth >= 0 && xml.Depth > containerDepth)
                            throw BadWorkbook("HostileWorkbook", "Celle o stringhe annidate non ammesse.");
                        containerDepth = xml.IsEmptyElement ? -1 : xml.Depth;
                        containerNodes = 0;
                        containerText = 0;
                    }

                    if (xml.LocalName == "f")
                        throw BadWorkbook("FormulaNotAllowed", "Il workbook contiene formule non ammesse.");
                    if (xml.LocalName == "Relationship" && string.Equals(xml.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                        throw BadWorkbook("ExternalReferenceNotAllowed", "Riferimenti esterni non ammessi.");
                    if (xml.HasAttributes)
                    {
                        while (xml.MoveToNextAttribute())
                            if (xml.Value.Length > 2048)
                                throw BadWorkbook("XmlLimitExceeded", "Attributo XML oltre il limite.");
                        xml.MoveToElement();
                    }
                }

                if (containerDepth >= 0)
                {
                    if (++containerNodes > 256)
                        throw BadWorkbook("XmlLimitExceeded", "Struttura cella oltre il limite.");
                    if (xml.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
                    {
                        containerText += xml.Value.Length;
                        if (containerText > limits.MaxCellCharacters + 128)
                            throw BadWorkbook("CellTooLong", "Contenuto cella oltre il limite.");
                    }

                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == containerDepth)
                        containerDepth = -1;
                }
            }
        }
    }
}
