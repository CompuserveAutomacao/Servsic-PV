using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço de envio direto de dados para a impressora térmica via Spooler do Windows (RAW / ESC-POS).
/// Não depende de drivers gráficos complexos e é compatível com qualquer impressora de bobina.
/// </summary>
public class ImpressaoTermicaService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr pPrinterEnum, int cbBuf, out int pcbNeeded, out int pcReturned);

    private const int PRINTER_ENUM_LOCAL = 0x00000002;
    private const int PRINTER_ENUM_CONNECTIONS = 0x00000004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        public string pServerName;
        public string pPrinterName;
        public string pShareName;
        public string pPortName;
        public string pDriverName;
        public string pComment;
        public string pLocation;
        public IntPtr pDevMode;
        public string pSepFile;
        public string pPrintProcessor;
        public string pDatatype;
        public string pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    /// <summary>
    /// Lista os nomes de todas as impressoras instaladas no sistema operacional
    /// </summary>
    public List<string> ListarImpressorasInstaladas()
    {
        var lista = new List<string>();

        try
        {
            int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out int cbNeeded, out _);

            if (cbNeeded > 0)
            {
                IntPtr pAddr = Marshal.AllocHGlobal(cbNeeded);
                try
                {
                    if (EnumPrinters(flags, null, 2, pAddr, cbNeeded, out _, out int count))
                    {
                        var info = new PRINTER_INFO_2();
                        int structSize = Marshal.SizeOf(info);

                        for (int i = 0; i < count; i++)
                        {
                            IntPtr current = new(pAddr.ToInt64() + (i * structSize));
                            var pInfo = (PRINTER_INFO_2)Marshal.PtrToStructure(current, typeof(PRINTER_INFO_2))!;
                            if (!string.IsNullOrWhiteSpace(pInfo.pPrinterName) && !lista.Contains(pInfo.pPrinterName))
                            {
                                lista.Add(pInfo.pPrinterName);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pAddr);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImpressaoTermicaService] Erro ao enumerar impressoras: {ex.Message}");
        }

        return lista;
    }

    /// <summary>
    /// Envia o texto formatado para a impressora térmica selecionada via RAW Spooler
    /// </summary>
    public (bool Sucesso, string? Mensagem) ImprimirTexto(string nomeImpressora, string texto, bool cortarPapel = true)
    {
        if (string.IsNullOrWhiteSpace(nomeImpressora))
        {
            return (false, "Nenhuma impressora foi selecionada nas configurações.");
        }

        try
        {
            // Converte o texto para bytes usando codepage compatível (CP850 / CP860 / Latin1)
            // para manter acentuação legível nas impressoras térmicas
            Encoding enc;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                enc = Encoding.GetEncoding(850); // Padrão MS-DOS/ESC-POS Brasil
            }
            catch
            {
                enc = Encoding.Latin1;
            }

            // Normaliza quebras de linha para CRLF
            string textoNormalizado = texto.Replace("\r\n", "\n").Replace("\n", "\r\n");

            var bytesTexto = enc.GetBytes(textoNormalizado);
            var ms = new MemoryStream();

            // Comando ESC/POS: Inicializar impressora (ESC @)
            ms.WriteByte(0x1B);
            ms.WriteByte(0x40);

            // Conteúdo do Cupom
            ms.Write(bytesTexto, 0, bytesTexto.Length);

            // Avanço de papel (4 linhas para sair da guilhotina)
            ms.WriteByte(0x0A);
            ms.WriteByte(0x0A);
            ms.WriteByte(0x0A);
            ms.WriteByte(0x0A);

            // Corte de papel (Guilhotina)
            if (cortarPapel)
            {
                // ESC m (Epson / Bematech)
                ms.WriteByte(0x1B);
                ms.WriteByte(0x6D);

                // GS V 66 0 (Corte parcial com avanço padrão ESC/POS)
                ms.WriteByte(0x1D);
                ms.WriteByte(0x56);
                ms.WriteByte(0x42);
                ms.WriteByte(0x00);
            }

            byte[] bufferEnvio = ms.ToArray();
            return EnviarBytesRaw(nomeImpressora, bufferEnvio, "Cupom Nao Fiscal ServSic");
        }
        catch (Exception ex)
        {
            return (false, $"Falha ao preparar impressão: {ex.Message}");
        }
    }

    private (bool Sucesso, string? Mensagem) EnviarBytesRaw(string szPrinterName, byte[] pBytes, string szDocName)
    {
        IntPtr hPrinter = IntPtr.Zero;
        IntPtr pUnmanagedBytes = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(szPrinterName, out hPrinter, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                return (false, $"Não foi possível abrir a impressora '{szPrinterName}'. Código de erro Windows: {err}. Verifique se está conectada e ligada.");
            }

            var di = new DOCINFOA
            {
                pDocName = szDocName,
                pDataType = "RAW"
            };

            if (!StartDocPrinter(hPrinter, 1, di))
            {
                int err = Marshal.GetLastWin32Error();
                return (false, $"Falha ao iniciar documento de impressão. Erro Windows: {err}.");
            }

            if (!StartPagePrinter(hPrinter))
            {
                EndDocPrinter(hPrinter);
                return (false, "Falha ao iniciar página de impressão.");
            }

            pUnmanagedBytes = Marshal.AllocCoTaskMem(pBytes.Length);
            Marshal.Copy(pBytes, 0, pUnmanagedBytes, pBytes.Length);

            bool bSuccess = WritePrinter(hPrinter, pUnmanagedBytes, pBytes.Length, out _);

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);

            if (!bSuccess)
            {
                int err = Marshal.GetLastWin32Error();
                return (false, $"Erro ao transmitir dados para a impressora. Erro Windows: {err}.");
            }

            return (true, null);
        }
        finally
        {
            if (pUnmanagedBytes != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pUnmanagedBytes);

            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
        }
    }
}
