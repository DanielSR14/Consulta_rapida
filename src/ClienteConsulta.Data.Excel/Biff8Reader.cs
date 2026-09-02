using System.Globalization;
using System.Text;

namespace ClienteConsulta.Data.Excel;

/// <summary>
/// Leitor mínimo de BIFF8 (o formato binário do .xls antigo) — extrai só o texto das células
/// (rótulo/valor) de que <see cref="DominioReportImporter"/> precisa, ignorando formatação,
/// mesclagem de células, fontes etc.
/// </summary>
/// <remarks>
/// Existe porque tanto ExcelDataReader quanto NPOI (<c>HSSFWorkbook</c>) falham silenciosamente
/// em exports reais da Domínio no modelo "Completo" — o export contém uma quantidade muito acima
/// do normal de registros MERGECELLS (um por célula de indentação, em vez de consolidados), o que
/// aparentemente atropela o processamento de registros dessas bibliotecas: nenhuma delas lança
/// exceção, mas ambas terminam reportando 0 planilhas/0 colunas, mesmo com o arquivo íntegro
/// (confirmado inspecionando os bytes brutos: BOF/BOUNDSHEET/SST corretos, arquivo não corrompido,
/// não protegido por senha). Este leitor cobre só os registros que aparecem nesse tipo de export
/// (BOF/EOF, SST+CONTINUE, LABELSST, BLANK, NUMBER) e ignora tudo o mais avançando pelo tamanho do
/// registro — não tenta ser um leitor de BIFF8 completo.
/// </remarks>
internal static class Biff8Reader
{
    private const int OpBof = 0x0809;
    private const int OpEof = 0x000A;
    private const int OpContinue = 0x003C;
    private const int OpSst = 0x00FC;
    private const int OpLabelSst = 0x00FD;
    private const int OpBlank = 0x0201;
    private const int OpNumber = 0x0203;
    private const int WorksheetBofDocType = 0x0010;

    /// <summary>Extrai a primeira planilha como uma grade de texto (linha → colunas), célula vazia = "".</summary>
    public static List<List<string>> ReadFirstSheetGrid(byte[] workbookStream)
    {
        var records = SplitRecords(workbookStream);
        var strings = ParseSharedStrings(records);
        var grid = new List<List<string>>();

        var inWorksheet = false;
        foreach (var record in records)
        {
            switch (record.Opcode)
            {
                case OpBof:
                    inWorksheet = ReadU16(record.Data, 2) == WorksheetBofDocType;
                    break;
                case OpEof when inWorksheet:
                    return grid; // só nos interessa a primeira planilha.
                case OpLabelSst when inWorksheet:
                    {
                        var sstIndex = ReadU32(record.Data, 6);
                        var text = sstIndex < (uint)strings.Count ? strings[(int)sstIndex] : "";
                        SetCell(grid, ReadU16(record.Data, 0), ReadU16(record.Data, 2), text);
                        break;
                    }
                case OpNumber when inWorksheet:
                    {
                        var value = BitConverter.ToDouble(record.Data, 6);
                        SetCell(grid, ReadU16(record.Data, 0), ReadU16(record.Data, 2),
                            value.ToString("0.################", CultureInfo.InvariantCulture));
                        break;
                    }
                case OpBlank when inWorksheet:
                    SetCell(grid, ReadU16(record.Data, 0), ReadU16(record.Data, 2), "");
                    break;
            }
        }

        return grid;
    }

    private static void SetCell(List<List<string>> grid, int row, int col, string value)
    {
        while (grid.Count <= row)
            grid.Add([]);
        var line = grid[row];
        while (line.Count <= col)
            line.Add("");
        line[col] = value;
    }

    private readonly record struct RawRecord(int Opcode, byte[] Data);

    private static List<RawRecord> SplitRecords(byte[] buf)
    {
        var list = new List<RawRecord>();
        var pos = 0;
        while (pos + 4 <= buf.Length)
        {
            var opcode = ReadU16(buf, pos);
            var length = ReadU16(buf, pos + 2);
            var start = pos + 4;
            if (start + length > buf.Length)
                break;

            var data = new byte[length];
            Array.Copy(buf, start, data, 0, length);
            list.Add(new RawRecord(opcode, data));
            pos = start + length;
        }

        return list;
    }

    /// <summary>
    /// Monta a Shared String Table (SST). O registro SST costuma continuar em registros CONTINUE
    /// subsequentes; quando isso corta uma string no meio do array de caracteres, o CONTINUE
    /// reintroduz 1 byte de flags (só o bit de compressão) antes de retomar os caracteres — é a
    /// parte mais delicada do formato, tratada em <see cref="SegmentedCursor.ReadXlUnicodeString"/>.
    /// </summary>
    private static List<string> ParseSharedStrings(List<RawRecord> records)
    {
        var sstIndex = records.FindIndex(r => r.Opcode == OpSst);
        if (sstIndex < 0)
            return [];

        var segments = new List<byte[]> { records[sstIndex].Data };
        var i = sstIndex + 1;
        while (i < records.Count && records[i].Opcode == OpContinue)
        {
            segments.Add(records[i].Data);
            i++;
        }

        var cursor = new SegmentedCursor(segments);
        cursor.Skip(4); // total de referências às strings — não usamos.
        var uniqueCount = cursor.ReadU32();

        var result = new List<string>((int)Math.Min(uniqueCount, 100_000));
        for (var s = 0; s < uniqueCount; s++)
            result.Add(cursor.ReadXlUnicodeString());

        return result;
    }

    private static ushort ReadU16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static uint ReadU32(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    /// <summary>Lê por cima de vários registros (SST + seus CONTINUE) como se fossem um único buffer contínuo.</summary>
    private sealed class SegmentedCursor(List<byte[]> segments)
    {
        private int _segmentIndex;
        private int _offset;

        private bool AtSegmentEnd => _segmentIndex < segments.Count && _offset >= segments[_segmentIndex].Length;

        public void Skip(int count)
        {
            for (var k = 0; k < count; k++)
                ReadByte();
        }

        private byte ReadByte()
        {
            while (_segmentIndex < segments.Count && _offset >= segments[_segmentIndex].Length)
            {
                _segmentIndex++;
                _offset = 0;
            }

            var value = segments[_segmentIndex][_offset];
            _offset++;
            return value;
        }

        public ushort ReadU16() => (ushort)(ReadByte() | (ReadByte() << 8));

        public uint ReadU32() => (uint)(ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24));

        public string ReadXlUnicodeString()
        {
            var charCount = ReadU16();
            var flags = ReadByte();
            var highByte = (flags & 0x1) != 0;
            var hasExtRst = (flags & 0x4) != 0;
            var hasRichRuns = (flags & 0x8) != 0;

            var runCount = hasRichRuns ? ReadU16() : (ushort)0;
            var extRstByteCount = hasExtRst ? ReadU32() : 0u;

            var sb = new StringBuilder(charCount);
            var remaining = charCount;
            while (remaining > 0)
            {
                if (AtSegmentEnd)
                {
                    // Cruzou para o próximo CONTINUE no meio do array de caracteres: só o bit de
                    // compressão se repete aqui, os outros flags (rich text/ext) não.
                    highByte = (ReadByte() & 0x1) != 0;
                }

                sb.Append(highByte ? (char)ReadU16() : (char)ReadByte());
                remaining--;
            }

            if (hasRichRuns)
                Skip(runCount * 4);
            if (hasExtRst)
                Skip((int)extRstByteCount);

            return sb.ToString();
        }
    }
}
